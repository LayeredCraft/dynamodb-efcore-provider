namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata;

/// <summary>Identifies the DynamoDB secondary index type configured for an EF index.</summary>
public enum DynamoSecondaryIndexKind
{
    /// <summary>Configures the index as a global secondary index (GSI).</summary>
    Global,

    /// <summary>Configures the index as a local secondary index (LSI).</summary>
    Local,
}
