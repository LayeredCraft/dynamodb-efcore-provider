using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design.Internal;
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

        foreach (var generatedFile in generatedFiles)
            generatedFile.Code = RewriteLegacyInterceptLocations(
                RewriteGeneratedFilePreamble(RewriteExecutorPreamble(generatedFile.Code)),
                compilation,
                cancellationToken);

        return generatedFiles;
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

    private static string RewriteExecutorPreamble(string code)
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
                "EF Core's precompiled-query executor template changed.");

        var rewrittenCode = RelationalExecutorPreamble.Replace(code, replacement);
        if (rewrittenCode.Contains(
            "RelationalMaterializerLiftableConstantContext",
            StringComparison.Ordinal))
            throw new InvalidOperationException(
                "EF Core generated an unrecognized relational query-executor preamble.");

        return rewrittenCode;
    }

    [GeneratedRegex(
        "            var relationalModel = dbContext\\.Model\\.GetRelationalModel\\(\\);\\r?\\n"
        + "            var relationalTypeMappingSource = dbContext\\.GetService<IRelationalTypeMappingSource>\\(\\);\\r?\\n"
        + "            var materializerLiftableConstantContext = new RelationalMaterializerLiftableConstantContext\\(\\r?\\n"
        + "                dbContext\\.GetService<ShapedQueryCompilingExpressionVisitorDependencies>\\(\\),\\r?\\n"
        + "                dbContext\\.GetService<RelationalShapedQueryCompilingExpressionVisitorDependencies>\\(\\),\\r?\\n"
        + "                dbContext\\.GetService<RelationalCommandBuilderDependencies>\\(\\)\\);\\r?\\n")]
    private static partial Regex CreateRelationalExecutorPreambleRegex();

    [GeneratedRegex(
        "\\[InterceptsLocation\\(@\\\"(?<path>(?:[^\\\"]|\\\"\\\")*)\\\", (?<line>\\d+), (?<column>\\d+)\\)\\]")]
    private static partial Regex LegacyInterceptLocationRegex();

    [GeneratedRegex(
        "public InterceptsLocationAttribute\\(string filePath, int line, int column\\) \\{ \\}")]
    private static partial Regex LegacyInterceptLocationConstructorRegex();
}
