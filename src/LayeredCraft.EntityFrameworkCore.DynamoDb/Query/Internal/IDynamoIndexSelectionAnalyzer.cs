namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Analysis seam for DynamoDB index selection at query compile time.
/// </summary>
/// <remarks>
/// Invoked by <see cref="DynamoQueryTranslationPostprocessor"/> after the query has been
/// translated into a <c>SelectExpression</c> with discriminator predicates and projection
/// finalised, and before SQL generation. The default implementation handles only explicit
/// <c>.WithIndex()</c> hints; steps 7-8 supply a richer implementation inside the postprocessor.
/// The interface is registered in DI and injected through
/// <see cref="DynamoQueryTranslationPostprocessorFactory"/>, which keeps
/// <see cref="IQueryTranslationPostprocessorFactory"/> as the EF Core seam while still allowing
/// <c>ReplaceService&lt;IDynamoIndexSelectionAnalyzer, T&gt;()</c> to swap the analyzer.
/// </remarks>
internal interface IDynamoIndexSelectionAnalyzer
{
    /// <summary>Analyses the query context and returns an index-selection decision.</summary>
    /// <param name="context">Compile-time snapshot with pre-fetched runtime descriptors.</param>
    /// <returns>
    /// A <see cref="DynamoIndexSelectionDecision"/> that names the index to use (or <c>null</c>
    /// for the base table) and includes any diagnostic observations.
    /// </returns>
    DynamoIndexSelectionDecision Analyze(DynamoIndexAnalysisContext context);
}
