namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Analysis seam for DynamoDB index selection at query compile time.
/// </summary>
/// <remarks>
/// Invoked by <c>DynamoQueryTranslationPostprocessor</c> after the query has been
/// translated into a <c>SelectExpression</c> with discriminator predicates and projection
/// finalised, and before SQL generation. The default implementation handles only explicit
/// <c>.WithIndex()</c> hints; steps 7-8 supply a richer implementation inside the postprocessor.
/// The interface is registered in DI and injected through
/// <c>DynamoQueryTranslationPostprocessorFactory</c>, which keeps
/// <c>IQueryTranslationPostprocessorFactory</c> as the EF Core seam while still allowing
/// <c>ReplaceService&lt;IDynamoIndexSelectionAnalyzer, T&gt;()</c> to swap the analyzer.
/// </remarks>
internal interface IDynamoIndexSelectionAnalyzer
{
    /// <summary>Analyses the query context and returns an index-selection decision.</summary>
    /// <returns>
    /// A <c>DynamoIndexSelectionDecision</c> that names the index to use (or <c>null</c>
    /// for the base table) and includes any diagnostic observations.
    /// </returns>
    DynamoIndexSelectionDecision Analyze(DynamoIndexAnalysisContext context);
}
