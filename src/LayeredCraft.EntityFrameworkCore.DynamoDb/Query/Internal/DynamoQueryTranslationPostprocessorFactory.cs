using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Factory that creates <see cref="DynamoQueryTranslationPostprocessor"/> instances.
/// Registering <see cref="IQueryTranslationPostprocessorFactory"/> is the single EF Core seam
/// for provider-specific query analysis. <see cref="IDynamoIndexSelectionAnalyzer"/> is injected
/// here so callers can replace it via <c>ReplaceService&lt;IDynamoIndexSelectionAnalyzer, T&gt;()</c>
/// without needing to replace the entire factory — enabling steps 7–8 auto-selection and test
/// substitution.
/// </summary>
internal sealed class DynamoQueryTranslationPostprocessorFactory(
    QueryTranslationPostprocessorDependencies dependencies,
    IDynamoIndexSelectionAnalyzer indexSelectionAnalyzer)
    : IQueryTranslationPostprocessorFactory
{
    /// <summary>Creates a <see cref="DynamoQueryTranslationPostprocessor"/> for the current query compilation.</summary>
    /// <param name="queryCompilationContext">The compilation context for the query being compiled.</param>
    /// <returns>A configured <see cref="DynamoQueryTranslationPostprocessor"/> for the given compilation context.</returns>
    public QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext)
        => new DynamoQueryTranslationPostprocessor(
            dependencies,
            (DynamoQueryCompilationContext)queryCompilationContext,
            indexSelectionAnalyzer);
}
