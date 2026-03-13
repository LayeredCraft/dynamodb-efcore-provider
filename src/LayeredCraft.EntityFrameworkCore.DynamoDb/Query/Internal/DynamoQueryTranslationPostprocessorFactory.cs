using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Factory that creates <c>DynamoQueryTranslationPostprocessor</c> instances.
/// Registering <c>IQueryTranslationPostprocessorFactory</c> is the single EF Core seam
/// for provider-specific query analysis. <c>IDynamoIndexSelectionAnalyzer</c> is injected
/// here so callers can replace it via <c>ReplaceService&lt;IDynamoIndexSelectionAnalyzer, T&gt;()</c>
/// without needing to replace the entire factory — enabling steps 7–8 auto-selection and test
/// substitution.
/// </summary>
internal sealed class DynamoQueryTranslationPostprocessorFactory(
    QueryTranslationPostprocessorDependencies dependencies,
    IDynamoIndexSelectionAnalyzer indexSelectionAnalyzer)
    : IQueryTranslationPostprocessorFactory
{
    /// <summary>Creates a <c>DynamoQueryTranslationPostprocessor</c> for the current query compilation.</summary>
    /// <returns>A configured <c>DynamoQueryTranslationPostprocessor</c> for the given compilation context.</returns>
    public QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext)
        => new DynamoQueryTranslationPostprocessor(
            dependencies,
            (DynamoQueryCompilationContext)queryCompilationContext,
            indexSelectionAnalyzer);
}
