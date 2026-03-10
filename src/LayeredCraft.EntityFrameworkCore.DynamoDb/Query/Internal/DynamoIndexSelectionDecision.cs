namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// The outcome of index-selection analysis for a single compiled query.
/// Returned by <see cref="IDynamoIndexSelectionAnalyzer.Analyze"/>.
/// </summary>
internal sealed record DynamoIndexSelectionDecision(
    string? SelectedIndexName,
    DynamoIndexSelectionReason Reason,
    IReadOnlyList<DynamoQueryDiagnostic> Diagnostics);

/// <summary>Describes why a particular index (or no index) was selected.</summary>
internal enum DynamoIndexSelectionReason
{
    /// <summary>No index was selected; the query will use the base table.</summary>
    NoSelection,

    /// <summary>The index was selected because the caller supplied an explicit <c>.WithIndex()</c> hint.</summary>
    ExplicitHint,

    /// <summary>
    /// The index was chosen automatically by the analyzer.
    /// Used in steps 7–8 when structural analysis is implemented.
    /// </summary>
    AutoSelected,
}
