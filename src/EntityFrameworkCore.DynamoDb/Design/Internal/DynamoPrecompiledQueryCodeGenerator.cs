using System.Reflection;
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

        var modelClrTypeNames = CollectModelClrTypeNames(dbContext.Model);

        foreach (var generatedFile in generatedFiles)
            generatedFile.Code = RewriteUnsafeAccessorTypes(
                RewriteLegacyInterceptLocations(
                    RewriteGeneratedFilePreamble(
                        RewriteExecutorPreamble(generatedFile.Code, generatedFile.Path)),
                    compilation,
                    cancellationToken),
                modelClrTypeNames);

        return generatedFiles;
    }

    /// <summary>
    ///     Every CLR type EF Core maps as an entity type or a complex type (recursively through
    ///     complex properties and complex collections), as the accessor-name prefix EF Core's
    ///     translator derives from it (<c>UnsafeAccessor_{Namespace_with_underscores}_{TypeName}_</c>)
    ///     paired with its fully qualified name.
    /// </summary>
    /// <remarks>
    ///     EF Core's precompiled-query generator accumulates <c>[UnsafeAccessor]</c> declarations
    ///     across every source file in the compilation in one unscoped, per-run set (an upstream
    ///     defect: private, non-virtual state on <c>LinqToCSharpSyntaxTranslator</c> that is never
    ///     reset between files - see the fix comment on <see cref="RewriteUnsafeAccessorTypes" />).
    ///     A generated file can therefore contain an accessor for an entity type that only some
    ///     other file's queries touch, and whose namespace this file never imports. Keyed by
    ///     accessor-name prefix rather than by simple type name, so two model types that happen to
    ///     share a simple name across namespaces (see <see cref="RewriteUnsafeAccessorTypes" />)
    ///     each still resolve to their own fully qualified name.
    /// </remarks>
    private static List<(string AccessorNamePrefix, string SimpleName, string QualifiedName)> CollectModelClrTypeNames(
        IModel model)
    {
        var names = new List<(string, string, string)>();
        var seenTypes = new HashSet<Type>();

        foreach (var entityType in model.GetEntityTypes())
            AddTypeBase(entityType);

        return names;

        void AddTypeBase(IReadOnlyTypeBase typeBase)
        {
            if (!AddType(typeBase.ClrType))
                return;

            foreach (var complexProperty in typeBase.GetComplexProperties())
                AddTypeBase(complexProperty.ComplexType);
        }

        bool AddType(Type type)
        {
            if (type.Namespace is null || !seenTypes.Add(type))
                return false;

            names.Add((
                $"UnsafeAccessor_{type.Namespace.Replace('.', '_')}_{type.Name}_",
                type.Name,
                $"global::{type.Namespace}.{type.Name}"));
            return true;
        }
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
    ///     Fully qualifies the parameter and return types of every <c>[UnsafeAccessor]</c>-attributed
    ///     method declaration in the generated file.
    /// </summary>
    /// <remarks>
    ///     EF Core's precompiled-query generator collects these declarations in a single,
    ///     per-compilation-run set on an internal, non-virtual translator instance rather than
    ///     resetting it per generated file (upstream defect - not something a provider can fix via
    ///     inheritance). A file can therefore contain an accessor for an entity type that only some
    ///     other file's own queries reference, and this file's `using` directives never import that
    ///     type's namespace. Fully qualifying every accessor's types, using the CLR type names
    ///     collected from the model itself, makes each file self-contained and correct regardless of
    ///     which other files happened to generate first, and disambiguates two model types that
    ///     happen to share a simple name.
    /// </remarks>
    private static string RewriteUnsafeAccessorTypes(
        string code,
        IReadOnlyList<(string AccessorNamePrefix, string SimpleName, string QualifiedName)> modelClrTypeNames)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();
        var rewriter = new UnsafeAccessorTypeQualifyingRewriter(modelClrTypeNames);
        var rewrittenRoot = rewriter.Visit(root);
        return rewrittenRoot == root ? code : rewrittenRoot.ToFullString();
    }

    private sealed class UnsafeAccessorTypeQualifyingRewriter(
        IReadOnlyList<(string AccessorNamePrefix, string SimpleName, string QualifiedName)> modelClrTypeNames)
        : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (!HasUnsafeAccessorAttribute(node))
                return base.VisitMethodDeclaration(node);

            // Every [UnsafeAccessor] method EF Core generates here is for exactly one model CLR
            // type: its own name encodes that type unambiguously
            // (UnsafeAccessor_{Namespace_with_underscores}_{TypeName}_{member}), even when that
            // type's simple name collides with another model type's. Resolve this method's own
            // type from its own name, rather than by simple name across the whole file, so a
            // collision cannot cause the wrong type - or no type - to be substituted.
            var methodName = node.Identifier.Text;
            var declaringType = modelClrTypeNames.FirstOrDefault(entry
                => methodName.StartsWith(entry.AccessorNamePrefix, StringComparison.Ordinal));
            if (declaringType.QualifiedName is null)
                return base.VisitMethodDeclaration(node);

            var rewrittenReturnType = (TypeSyntax)VisitTypeIfKnown(node.ReturnType, declaringType);
            var rewrittenParameters = node.ParameterList.Parameters
                .Select(parameter => parameter.Type is { } parameterType
                    ? parameter.WithType((TypeSyntax)VisitTypeIfKnown(parameterType, declaringType))
                    : parameter);

            return node
                .WithReturnType(rewrittenReturnType)
                .WithParameterList(node.ParameterList.WithParameters([.. rewrittenParameters]));
        }

        private static bool HasUnsafeAccessorAttribute(MethodDeclarationSyntax method) => method.AttributeLists
            .SelectMany(static list => list.Attributes)
            .Any(static attribute => attribute.Name.ToString() is "UnsafeAccessor" or "UnsafeAccessorAttribute");

        // Only rewrites the type shapes EF Core's translator actually emits here (a bare type name,
        // a nullable annotation over one, a ref return, or an array element type) - not full general
        // type-syntax traversal, since only entity/complex CLR types (never a generic or pointer
        // type) appear as unsafe-accessor parameter/return types. Rewrites only occurrences of the
        // method's own declaring type, identified above - never any other model type's simple name
        // that might otherwise appear (for example a constructor accessor's own parameter list).
        private static SyntaxNode VisitTypeIfKnown(
            TypeSyntax type,
            (string AccessorNamePrefix, string SimpleName, string QualifiedName) declaringType) => type switch
        {
            IdentifierNameSyntax identifier when identifier.Identifier.Text == declaringType.SimpleName =>
                SyntaxFactory.ParseTypeName(declaringType.QualifiedName).WithTriviaFrom(identifier),
            NullableTypeSyntax nullable =>
                nullable.WithElementType((TypeSyntax)VisitTypeIfKnown(nullable.ElementType, declaringType)),
            RefTypeSyntax refType =>
                refType.WithType((TypeSyntax)VisitTypeIfKnown(refType.Type, declaringType)),
            ArrayTypeSyntax array =>
                array.WithElementType((TypeSyntax)VisitTypeIfKnown(array.ElementType, declaringType)),
            _ => type
        };
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
