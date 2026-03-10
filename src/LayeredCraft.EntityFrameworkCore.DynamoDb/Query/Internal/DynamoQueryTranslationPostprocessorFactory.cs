using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Factory that creates <see cref="DynamoQueryTranslationPostprocessor"/> instances, injecting
/// the <see cref="IDynamoIndexSelectionAnalyzer"/> seam so tests and advanced integrations can
/// substitute alternative analyzer implementations.
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
