namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Default <see cref="IDynamoIndexSelectionAnalyzer"/> implementation that handles explicit
/// <c>.WithIndex()</c> hints and returns <see cref="DynamoIndexSelectionReason.NoSelection"/>
/// when no hint is present.
/// </summary>
/// <remarks>
/// This implementation covers step 6 of the secondary-index feature: wiring the analysis seam
/// into the compilation pipeline. Steps 7–8 will replace or extend this class with structural
/// query analysis and conservative auto-index selection.
/// </remarks>
internal sealed class DynamoDefaultIndexSelectionAnalyzer : IDynamoIndexSelectionAnalyzer
{
    /// <summary>
    /// Validates and returns the explicit index hint when present; returns <c>NoSelection</c>
    /// when the caller did not supply a <c>.WithIndex()</c> hint.
    /// </summary>
    /// <param name="context">
    /// Compile-time snapshot including the explicit hint and pre-fetched runtime descriptors.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an explicit hint names an index that is not registered for the queried table
    /// and entity type.
    /// </exception>
    public DynamoIndexSelectionDecision Analyze(DynamoIndexAnalysisContext context)
    {
        if (context.ExplicitIndexHint is not { } indexName)
            return new DynamoIndexSelectionDecision(null, DynamoIndexSelectionReason.NoSelection, []);

        // When no candidates are available (design-time; runtime model not yet built), skip
        // validation to avoid false negatives during model building and tooling scenarios.
        if (context.CandidateDescriptors.Count == 0)
            return new DynamoIndexSelectionDecision(indexName, DynamoIndexSelectionReason.ExplicitHint, []);

        var indexExists = context.CandidateDescriptors.Any(d => d.IndexName == indexName);

        if (!indexExists)
            throw new InvalidOperationException(
                $"Index '{indexName}' is not configured on table '{context.SelectExpression.TableName}'. "
                + "Use HasGlobalSecondaryIndex or HasLocalSecondaryIndex to register the "
                + "index before using WithIndex.");

        return new DynamoIndexSelectionDecision(indexName, DynamoIndexSelectionReason.ExplicitHint, []);
    }
}
