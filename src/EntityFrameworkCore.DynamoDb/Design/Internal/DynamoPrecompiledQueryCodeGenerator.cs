using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
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
            generatedFile.Code = RewriteExecutorPreamble(generatedFile.Code);

        return generatedFiles;
    }

    private static string RewriteExecutorPreamble(string code)
    {
        var lineEnding = code.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var replacement = string.Join(
                lineEnding,
                "            var materializerLiftableConstantContext = new MaterializerLiftableConstantContext(",
                "                dbContext.GetService<ShapedQueryCompilingExpressionVisitorDependencies>());")
            + lineEnding;

        var rewrittenCode = RelationalExecutorPreamble.Replace(code, replacement);
        if (ReferenceEquals(rewrittenCode, code))
            throw new InvalidOperationException(
                "EF Core's precompiled-query executor template changed.");

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
}
