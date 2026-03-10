namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// A diagnostic observation produced during index-selection analysis.
/// Collected as part of <see cref="DynamoIndexSelectionDecision.Diagnostics"/>.
/// </summary>
/// <remarks>
/// These diagnostics are wired to EF structured logging events in step 10.
/// For now they are accumulated on the decision and available for testing/inspection.
/// </remarks>
internal sealed record DynamoQueryDiagnostic(
    DynamoQueryDiagnosticLevel Level,
    string Code,
    string Message);

/// <summary>Severity level for a <see cref="DynamoQueryDiagnostic"/>.</summary>
internal enum DynamoQueryDiagnosticLevel
{
    Information,
    Warning,
}
