namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Classifies whether a finalized DynamoDB read query is scan-like.</summary>
internal sealed record DynamoScanQueryClassification(
    bool IsScanLike,
    string SourceDescription,
    string Reason,
    string Message);
