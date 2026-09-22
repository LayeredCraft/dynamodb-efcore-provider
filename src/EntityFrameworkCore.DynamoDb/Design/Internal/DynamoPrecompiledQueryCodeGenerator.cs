using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding;

namespace EntityFrameworkCore.DynamoDb.Design.Internal;

/// <summary>Generates precompiled-query interceptors without relational runtime dependencies.</summary>
public sealed partial class DynamoPrecompiledQueryCodeGenerator : PrecompiledQueryCodeGenerator
{
    private static readonly Regex RelationalExecutorPreamble =
        CreateRelationalExecutorPreambleRegex();

    /// <inheritdoc />
    public override IReadOnlyList<ScaffoldedFile> GeneratePrecompiledQueries(
        Compilation compilation,
        SyntaxGenerator syntaxGenerator,
        DbContext dbContext,
        IReadOnlyDictionary<MemberInfo, QualifiedName> memberAccessReplacements,
        List<QueryPrecompilationError> precompilationErrors,
        ISet<string> generatedFileNames,
        Assembly? additionalAssembly = null,
        string? suffix = null,
        CancellationToken cancellationToken = default)
    {
        var generatedFiles = base.GeneratePrecompiledQueries(
            compilation,
            syntaxGenerator,
            dbContext,
            memberAccessReplacements,
            precompilationErrors,
            generatedFileNames,
            additionalAssembly,
            suffix,
            cancellationToken);

        var accessorMembers = BuildAccessorMemberTable(dbContext.Model);

        foreach (var generatedFile in generatedFiles)
            generatedFile.Code = RewriteUnsafeAccessorTypes(
                RewriteLegacyInterceptLocations(
                    RewriteGeneratedFilePreamble(
                        RewriteExecutorPreamble(generatedFile.Code, generatedFile.Path)),
                    compilation,
                    cancellationToken),
                compilation,
                accessorMembers);

        return generatedFiles;
    }

    /// <summary>
    ///     A CLR member (field, method, or constructor) reachable from the <see cref="DbContext" />'s
    ///     model - either directly declared on an entity/complex type, or on a complex type reached
    ///     recursively through complex properties/collections - that EF Core's precompiled-query
    ///     generator could plausibly need an <c>[UnsafeAccessor]</c> for.
    /// </summary>
    private readonly record struct AccessorMember(
        UnsafeAccessorKind Kind,
        Type DeclaringType,
        FieldInfo? Field,
        MethodBase? Method,
        bool ForWrite)
    {
        public int ParameterCount => Kind switch
        {
            UnsafeAccessorKind.Method => Method!.GetParameters().Length,
            UnsafeAccessorKind.Constructor => Method!.GetParameters().Length,
            _ => 0
        };

        public string MemberName => Kind switch
        {
            UnsafeAccessorKind.Field => Field!.Name,
            UnsafeAccessorKind.Constructor => "<ctor>",
            _ => Method!.Name
        };
    }

    /// <summary>
    ///     Builds a table, keyed by the exact <c>[UnsafeAccessor]</c> method name EF Core's
    ///     translator would generate for each member, of every field/method/constructor declared
    ///     directly on a CLR type the model maps as an entity type or a complex type (recursively
    ///     through complex properties and complex collections).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         EF Core's precompiled-query generator accumulates <c>[UnsafeAccessor]</c>
    ///         declarations across every source file in the compilation in one unscoped, per-run set
    ///         (an upstream defect: private, non-virtual state on
    ///         <c>LinqToCSharpSyntaxTranslator</c> that is never reset between files). A generated
    ///         file can therefore contain an accessor for a member that only some other file's
    ///         queries touch, and whose declaring/member types this file's <c>using</c> directives
    ///         never import.
    ///     </para>
    ///     <para>
    ///         The generated accessor method's own name is the only correlation available once the
    ///         file has been generated (EF Core's internal per-member dictionaries that would give
    ///         an authoritative answer are private and never surfaced). That name is built by EF Core
    ///         as <c>UnsafeAccessor_{Namespace_with_underscores}_{DeclaringTypeName}_{MemberName}</c>
    ///         (plus <c>_Get</c>/<c>_Set</c> for fields) - replacing every <c>.</c> in the namespace
    ///         with <c>_</c> is lossy (<c>A_B.C</c> and <c>A.B_C</c> both encode to <c>A_B_C</c>), so
    ///         this table is built by recomputing that exact same (lossy) name for every reachable
    ///         member and grouping by it, rather than by trying to parse the namespace back out of a
    ///         generated name. A generated name that maps to more than one table entry is a real,
    ///         EF Core-generated ambiguity - see <c>ResolveAccessorMember</c>.
    ///     </para>
    /// </remarks>
    private static Dictionary<string, List<AccessorMember>> BuildAccessorMemberTable(IModel model)
    {
        const BindingFlags declaredInstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        var table = new Dictionary<string, List<AccessorMember>>(StringComparer.Ordinal);
        var seenTypes = new HashSet<Type>();

        foreach (var entityType in model.GetEntityTypes())
            AddTypeBase(entityType);

        return table;

        void AddTypeBase(IReadOnlyTypeBase typeBase)
        {
            if (!seenTypes.Add(typeBase.ClrType))
                return;

            AddMembers(typeBase.ClrType);

            foreach (var complexProperty in typeBase.GetComplexProperties())
                AddTypeBase(complexProperty.ComplexType);
        }

        void AddMembers(Type type)
        {
            foreach (var field in type.GetFields(declaredInstanceMembers))
            {
                var memberNamePart = GetFieldAccessorNamePart(field);
                Add(BuildAccessorMethodName(type, memberNamePart, "_Get"),
                    new AccessorMember(UnsafeAccessorKind.Field, type, field, null, false));
                Add(BuildAccessorMethodName(type, memberNamePart, "_Set"),
                    new AccessorMember(UnsafeAccessorKind.Field, type, field, null, true));
            }

            foreach (var method in type.GetMethods(declaredInstanceMembers))
                Add(BuildAccessorMethodName(type, method.Name, suffix: null),
                    new AccessorMember(UnsafeAccessorKind.Method, type, null, method, false));

            foreach (var constructor in type.GetConstructors(declaredInstanceMembers))
                Add(BuildAccessorMethodName(type, "Ctor", suffix: null),
                    new AccessorMember(UnsafeAccessorKind.Constructor, type, null, constructor, false));
        }

        void Add(string accessorMethodName, AccessorMember member)
        {
            if (!table.TryGetValue(accessorMethodName, out var candidates))
                table[accessorMethodName] = candidates = [];
            candidates.Add(member);
        }
    }

    // Mirrors LinqToCSharpSyntaxTranslator.GetUnsafeAccessorDeclaration's backing-field-to-property-name
    // extraction exactly (an auto-property's compiler-generated "<Name>k__BackingField" becomes "Name"),
    // since that is the member-name component EF Core's generated accessor name encodes for fields.
    private static string GetFieldAccessorNamePart(FieldInfo field)
    {
        var name = field.Name;
        return name.Length > 0
            && name[0] == '<'
            && name.IndexOf(">k__BackingField", StringComparison.Ordinal) is > 1 and var backingFieldMarker
            ? name[1..backingFieldMarker]
            : name;
    }

    // Mirrors LinqToCSharpSyntaxTranslator.GetUnsafeAccessorDeclaration's own name-building exactly,
    // including its lossy namespace-to-underscore folding - see BuildAccessorMemberTable's remarks.
    private static string BuildAccessorMethodName(Type declaringType, string memberNamePart, string? suffix)
    {
        var name = new StringBuilder("UnsafeAccessor_");
        if (declaringType.Namespace is { } declaringNamespace)
            name.Append(declaringNamespace.Replace('.', '_')).Append('_');

        name.Append(declaringType.Name).Append('_').Append(memberNamePart);
        return suffix is null ? name.ToString() : name.Append(suffix).ToString();
    }

    private static string RewriteGeneratedFilePreamble(string code)
    {
        if (code.Contains("#nullable enable annotations", StringComparison.Ordinal))
            return code;

        var headerEnd = code.IndexOf('\n');
        if (headerEnd < 0)
            throw new InvalidOperationException(
                "EF Core generated an invalid interceptor file header.");

        return code.Insert(
            headerEnd + 1,
            "#nullable enable annotations\n"
            + "#nullable disable warnings\n"
            + "#pragma warning disable CS0162\n");
    }

    private static string RewriteLegacyInterceptLocations(
        string code,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        if (!LegacyInterceptLocationRegex().IsMatch(code))
            return code;

        var rewrittenCode = LegacyInterceptLocationRegex()
            .Replace(
                code,
                match =>
                {
                    var path = match.Groups["path"].Value.Replace("\"\"", "\"");
                    var syntaxTree = compilation.SyntaxTrees.SingleOrDefault(tree
                        => string.Equals(tree.FilePath, path, StringComparison.Ordinal));
                    if (syntaxTree is null)
                        throw new InvalidOperationException(
                            $"Could not find interceptor source file '{path}'.");

                    var line = int.Parse(match.Groups["line"].Value) - 1;
                    var column = int.Parse(match.Groups["column"].Value) - 1;
                    var textLine = syntaxTree.GetText(cancellationToken).Lines[line];
                    var position = textLine.Start + column;
                    var invocation =
                        syntaxTree
                            .GetRoot(cancellationToken)
                            .FindToken(position)
                            .Parent
                            ?.FirstAncestorOrSelf<InvocationExpressionSyntax>()
                        ?? throw new InvalidOperationException(
                            $"Could not resolve the intercepted call at '{path}:{line + 1}:{column + 1}'.");

#pragma warning disable RSEXPERIMENTAL004
                    var interceptableLocation =
                        compilation
                            .GetSemanticModel(syntaxTree)
                            .GetInterceptableLocation(invocation, cancellationToken)
                        ?? throw new InvalidOperationException(
                            $"Could not encode the intercepted call at '{path}:{line + 1}:{column + 1}'.");
                    return interceptableLocation.GetInterceptsLocationAttributeSyntax().ToString();
#pragma warning restore RSEXPERIMENTAL004
                });

        return LegacyInterceptLocationConstructorRegex()
            .Replace(
                rewrittenCode,
                "public InterceptsLocationAttribute(int version, string data) { }");
    }

    private static string RewriteExecutorPreamble(string code, string hintName)
    {
        var lineEnding = code.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var replacement = string.Join(
                lineEnding,
                "            var materializerLiftableConstantContext = new MaterializerLiftableConstantContext(",
                "                dbContext.GetService<ShapedQueryCompilingExpressionVisitorDependencies>());")
            + lineEnding;

        var matchCount = RelationalExecutorPreamble.Matches(code).Count;
        if (matchCount == 0)
            throw new InvalidOperationException(
                $"EF Core {typeof(PrecompiledQueryCodeGenerator).Assembly.GetName().Version} "
                + "generated an incompatible precompiled-query executor template. Expected preamble "
                + "starts with 'var relationalModel = dbContext.Model.GetRelationalModel();'.");

        var rewrittenCode = RelationalExecutorPreamble.Replace(code, replacement);
        if (rewrittenCode.Contains(
            "RelationalMaterializerLiftableConstantContext",
            StringComparison.Ordinal))
            throw new InvalidOperationException(
                "EF Core generated an unrecognized relational query-executor preamble "
                + $"(hint: {hintName}).");

        return rewrittenCode;
    }

    /// <summary>
    ///     Fully qualifies every CLR type occurring anywhere in the signature (receiver, return/
    ///     ref-return, and parameter types) of every <c>[UnsafeAccessor]</c>-attributed method
    ///     declaration in the generated file.
    /// </summary>
    /// <remarks>
    ///     EF Core's precompiled-query generator collects these declarations in a single,
    ///     per-compilation-run set on an internal, non-virtual translator instance rather than
    ///     resetting it per generated file (upstream defect - not something a provider can fix via
    ///     inheritance). A file can therefore contain an accessor for a member that only some other
    ///     file's own queries reference, and this file's <c>using</c> directives never import that
    ///     member's declaring or value type. Fully qualifying every CLR type referenced by such a
    ///     declaration - not merely its declaring type - using the actual reflected member located
    ///     via <c>ResolveAccessorMember</c>, makes each file self-contained and correct
    ///     regardless of which other files happened to generate first.
    /// </remarks>
    private static string RewriteUnsafeAccessorTypes(
        string code,
        Compilation compilation,
        IReadOnlyDictionary<string, List<AccessorMember>> accessorMembers)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();
        var rewriter = new UnsafeAccessorTypeQualifyingRewriter(compilation, accessorMembers);
        var rewrittenRoot = rewriter.Visit(root);
        return rewrittenRoot == root ? code : rewrittenRoot.ToFullString();
    }

    private sealed class UnsafeAccessorTypeQualifyingRewriter(
        Compilation compilation,
        IReadOnlyDictionary<string, List<AccessorMember>> accessorMembers) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (GetGeneratedAccessorKind(node) is not { } accessorKind)
                return base.VisitMethodDeclaration(node);

            if (!accessorMembers.TryGetValue(node.Identifier.Text, out var candidates) || candidates.Count == 0)
                return base.VisitMethodDeclaration(node);

            if (!TryResolveAccessorMember(node, accessorKind, candidates, out var member))
                return base.VisitMethodDeclaration(node);

            var parameters = node.ParameterList.Parameters;

            switch (member.Kind)
            {
                case UnsafeAccessorKind.Field:
                {
                    var rewrittenInstance = parameters[0]
                        .WithType(QualifyPreservingShape(parameters[0].Type!, member.DeclaringType));
                    return node
                        .WithReturnType(QualifyPreservingShape(node.ReturnType, member.Field!.FieldType))
                        .WithParameterList(
                            node.ParameterList.WithParameters(
                                SyntaxFactory.SingletonSeparatedList(rewrittenInstance)));
                }

                case UnsafeAccessorKind.Method:
                {
                    var method = (MethodInfo)member.Method!;
                    var methodParameters = method.GetParameters();
                    var rewrittenParameters = parameters.Select((parameter, index) => parameter.WithType(
                        QualifyPreservingShape(
                            parameter.Type!,
                            // Parameter 0 is the synthesized "instance" receiver, not one of the
                            // member's own declared parameters.
                            index == 0 ? member.DeclaringType : methodParameters[index - 1].ParameterType)));
                    return node
                        .WithReturnType(QualifyPreservingShape(node.ReturnType, method.ReturnType))
                        .WithParameterList(node.ParameterList.WithParameters([.. rewrittenParameters]));
                }

                case UnsafeAccessorKind.Constructor:
                {
                    var constructorParameters = ((ConstructorInfo)member.Method!).GetParameters();
                    var rewrittenParameters = parameters.Select((parameter, index) => parameter.WithType(
                        QualifyPreservingShape(parameter.Type!, constructorParameters[index].ParameterType)));
                    return node
                        .WithReturnType(QualifyPreservingShape(node.ReturnType, member.DeclaringType))
                        .WithParameterList(node.ParameterList.WithParameters([.. rewrittenParameters]));
                }

                default:
                    throw new UnreachableException($"Unexpected unsafe-accessor kind: {member.Kind}");
            }
        }

        /// <summary>
        ///     Identifies which reflected member a generated <c>[UnsafeAccessor]</c> method
        ///     declaration corresponds to, when EF Core's lossy namespace-to-underscore name folding
        ///     (see <see cref="BuildAccessorMemberTable" />) has produced more than one candidate for
        ///     the same generated name. Returns <see langword="false" /> only when no candidate at all
        ///     shares this declaration's <paramref name="accessorKind" /> - a gap in this provider's
        ///     own member-table traversal rather than an EF Core-generated collision - in which case
        ///     the caller leaves the declaration unmodified rather than risk a wrong rewrite.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The generated declaration's own <c>[UnsafeAccessor(UnsafeAccessorKind...)]</c>
        ///         attribute argument is authoritative and is checked first, before any name- or
        ///         shape-based narrowing: a hand-written zero-argument method named
        ///         <c>"Foo_Set"</c> and property <c>Foo</c>'s field-set accessor both generate the
        ///         name <c>"..._Foo_Set"</c> <i>and</i> both declarations end up with exactly one
        ///         declared parameter (the field accessor's synthesized "instance" receiver; the
        ///         method accessor's zero declared parameters plus its own synthesized receiver) - so
        ///         without checking <c>UnsafeAccessorKind</c> first, parameter-count narrowing alone
        ///         could select the wrong candidate with no ambiguity ever detected.
        ///     </para>
        ///     <para>
        ///         A field accessor's declaration shape never varies by the field's own type (always
        ///         exactly one "instance" parameter), so two colliding field candidates cannot be told
        ///         apart from the generated syntax at all - not even by which of two duplicate-looking
        ///         declarations appears first in the file, since that reflects EF Core's own internal,
        ///         unobservable iteration order, not anything recoverable from the generated text.
        ///         Method and constructor overloads sharing a generated name (and kind), however,
        ///         differ in parameter count, which the declaration's own parameter list already
        ///         states - a fact read directly off the generated syntax, not a guess - so those are
        ///         resolved by that count when it alone narrows the same-kind candidates to one.
        ///         Anything still ambiguous after that is a genuine, EF Core-generated naming
        ///         collision this provider cannot safely resolve, and is reported as such rather than
        ///         bound to an arbitrary candidate.
        ///     </para>
        /// </remarks>
        private static bool TryResolveAccessorMember(
            MethodDeclarationSyntax node,
            UnsafeAccessorKind accessorKind,
            List<AccessorMember> allCandidates,
            out AccessorMember member)
        {
            var candidates = allCandidates.Where(candidate => candidate.Kind == accessorKind).ToList();
            if (candidates.Count == 0)
            {
                member = default;
                return false;
            }

            if (candidates.Count == 1)
            {
                member = candidates[0];
                return true;
            }

            // The declaration's own parameter list includes the synthesized "instance" receiver in
            // addition to the member's declared parameters for methods, but not for constructors -
            // fields never reach here with more than one candidate (see remarks).
            var declaredParameterCount = node.ParameterList.Parameters.Count;
            var narrowed = candidates.Where(candidate => candidate.Kind switch
            {
                UnsafeAccessorKind.Method => candidate.ParameterCount + 1 == declaredParameterCount,
                UnsafeAccessorKind.Constructor => candidate.ParameterCount == declaredParameterCount,
                _ => false
            }).ToList();

            if (narrowed.Count == 1)
            {
                member = narrowed[0];
                return true;
            }

            throw new InvalidOperationException(
                $"EF Core generated an unsafe-accessor method named '{node.Identifier.Text}' (kind: "
                + $"{accessorKind}) that this provider cannot unambiguously attribute to a single CLR "
                + "member. EF Core encodes a member's declaring-type namespace into the generated name "
                + "by replacing '.' with '_', which collides for more than one of the following: "
                + string.Join(
                    "; ",
                    candidates.Select(candidate
                        => $"'{candidate.DeclaringType.FullName}.{candidate.MemberName}'"))
                + ". Rename one of these namespaces (or the affected members) so their encoded names no "
                + "longer collide.");
        }

        /// <summary>
        ///     Reads the <see cref="UnsafeAccessorKind" /> from a method declaration's
        ///     <c>[UnsafeAccessor(...)]</c> attribute, structurally - not inferred from the generated
        ///     method name - or <see langword="null" /> if it has no such attribute.
        /// </summary>
        private static UnsafeAccessorKind? GetGeneratedAccessorKind(MethodDeclarationSyntax method)
        {
            var argument = method.AttributeLists
                .SelectMany(static list => list.Attributes)
                .Where(static attribute => attribute.Name.ToString() is "UnsafeAccessor" or "UnsafeAccessorAttribute")
                .Select(static attribute => attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression)
                .OfType<MemberAccessExpressionSyntax>()
                .FirstOrDefault(static expression => expression.Expression is IdentifierNameSyntax
                {
                    Identifier.Text: nameof(UnsafeAccessorKind)
                });

            return argument is null
                ? null
                : Enum.Parse<UnsafeAccessorKind>(argument.Name.Identifier.Text);
        }

        // Preserves the `ref` (write accessors for fields) and reference-nullable annotation `?`
        // exactly as EF Core's translator emitted them for this position - both are syntax-level
        // decisions independent of the fully-qualified replacement substituted here (nullable
        // *reference* annotations have no runtime System.Type representation at all, and a nullable
        // *value* type - Nullable<T> - is rendered as part of clrType itself by TypeQualifier).
        private TypeSyntax QualifyPreservingShape(TypeSyntax originalSyntax, Type clrType)
        {
            // ParseTypeName produces a bare node with no leading/trailing trivia (no indentation, no
            // separating space before whatever token follows it, e.g. the parameter name or method
            // name) - carry over the original node's trivia so the replacement doesn't run into its
            // neighboring token.
            TypeSyntax rewritten = originalSyntax switch
            {
                RefTypeSyntax refType => refType.WithType(QualifyPreservingShape(refType.Type, clrType)),
                NullableTypeSyntax when clrType is { IsValueType: false } =>
                    SyntaxFactory.NullableType(TypeQualifier.ToFullyQualifiedTypeSyntax(compilation, clrType)),
                _ => TypeQualifier.ToFullyQualifiedTypeSyntax(compilation, clrType)
            };
            return rewritten.WithTriviaFrom(originalSyntax);
        }
    }

    /// <summary>
    ///     Renders a reflection <see cref="Type" /> as a fully qualified <see cref="TypeSyntax" />
    ///     (arrays, generic/constructed-generic types with their own type arguments, nested types,
    ///     nullable value types, and escaped/keyword-colliding identifiers all included), by
    ///     resolving it to a Roslyn <see cref="ITypeSymbol" /> against the query-building
    ///     compilation and formatting that symbol with <see cref="SymbolDisplayFormat.FullyQualifiedFormat" /> -
    ///     Roslyn's own established mechanism for this, rather than a hand-built string.
    /// </summary>
    private static class TypeQualifier
    {
        public static TypeSyntax ToFullyQualifiedTypeSyntax(Compilation compilation, Type type) =>
            SyntaxFactory.ParseTypeName(
                ResolveSymbol(compilation, type).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        private static ITypeSymbol ResolveSymbol(Compilation compilation, Type type)
        {
            if (type.IsArray)
                return compilation.CreateArrayTypeSymbol(
                    ResolveSymbol(compilation, type.GetElementType()!), type.GetArrayRank());

            if (Nullable.GetUnderlyingType(type) is { } underlyingType)
                return ResolveNamedSymbol(compilation, typeof(Nullable<>))
                    .Construct(ResolveSymbol(compilation, underlyingType));

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
                return ResolveNamedSymbol(compilation, type.GetGenericTypeDefinition())
                    .Construct([.. type.GetGenericArguments().Select(argument => ResolveSymbol(compilation, argument))]);

            return ResolveNamedSymbol(compilation, type);
        }

        private static INamedTypeSymbol ResolveNamedSymbol(Compilation compilation, Type type)
        {
            var metadataName = type.FullName
                ?? throw new InvalidOperationException(
                    $"Type '{type}' has no metadata name and cannot be fully qualified in a generated "
                    + "unsafe-accessor declaration.");

            return compilation.GetTypeByMetadataName(metadataName)
                ?? throw new InvalidOperationException(
                    $"Could not resolve type '{metadataName}' against the query-building compilation "
                    + "while fully qualifying a generated unsafe-accessor declaration.");
        }
    }

    [GeneratedRegex(
        " +var relationalModel = dbContext\\.Model\\.GetRelationalModel\\(\\);\\r?\\n"
        + " +var relationalTypeMappingSource = dbContext\\.GetService<IRelationalTypeMappingSource>\\(\\);\\r?\\n"
        + " +var materializerLiftableConstantContext = new RelationalMaterializerLiftableConstantContext\\(\\r?\\n"
        + " +dbContext\\.GetService<ShapedQueryCompilingExpressionVisitorDependencies>\\(\\),\\r?\\n"
        + " +dbContext\\.GetService<RelationalShapedQueryCompilingExpressionVisitorDependencies>\\(\\),\\r?\\n"
        + " +dbContext\\.GetService<RelationalCommandBuilderDependencies>\\(\\)\\);\\r?\\n")]
    private static partial Regex CreateRelationalExecutorPreambleRegex();

    [GeneratedRegex(
        "\\[InterceptsLocation\\(@\\\"(?<path>(?:[^\\\"]|\\\"\\\")*)\\\", (?<line>\\d+), (?<column>\\d+)\\)\\]")]
    private static partial Regex LegacyInterceptLocationRegex();

    [GeneratedRegex(
        "public InterceptsLocationAttribute\\(string filePath, int line, int column\\) \\{ \\}")]
    private static partial Regex LegacyInterceptLocationConstructorRegex();
}
