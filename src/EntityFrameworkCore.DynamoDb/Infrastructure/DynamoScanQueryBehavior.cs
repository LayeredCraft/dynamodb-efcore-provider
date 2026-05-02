namespace EntityFrameworkCore.DynamoDb.Infrastructure;

/// <summary>Controls how scan-like DynamoDB read queries are handled.</summary>
public enum DynamoScanQueryBehavior
{
    /// <summary>Throw before sending any DynamoDB request.</summary>
    Throw,

    /// <summary>Log a warning, then execute the query.</summary>
    Warn,

    /// <summary>Execute scan-like queries without logging a warning.</summary>
    Allow,
}
