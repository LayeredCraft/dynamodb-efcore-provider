namespace EntityFrameworkCore.DynamoDb.Metadata;

/// <summary>Describes which attributes a DynamoDB secondary index projects for query materialization.</summary>
public enum DynamoSecondaryIndexProjectionType
{
    /// <summary>Projects all attributes needed for full-entity materialization.</summary>
    All,

    /// <summary>Projects only the table key attributes and index key attributes.</summary>
    KeysOnly,

    /// <summary>Projects a configured subset of non-key attributes.</summary>
    Include,
}
