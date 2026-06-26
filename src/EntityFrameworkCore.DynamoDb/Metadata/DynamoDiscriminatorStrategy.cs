namespace EntityFrameworkCore.DynamoDb.Metadata;

/// <summary>Represents how entity discrimination is stored for DynamoDB mapped entities.</summary>
public enum DynamoDiscriminatorStrategy
{
    /// <summary>
    ///     Uses a dedicated discriminator attribute (for example, <c>$type</c>) to store entity type
    ///     identity; shared-table and inheritance queries use it for type filtering when needed.
    /// </summary>
    Attribute = 0
}
