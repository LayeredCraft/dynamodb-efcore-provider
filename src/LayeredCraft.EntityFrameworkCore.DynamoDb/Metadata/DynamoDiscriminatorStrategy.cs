namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata;

/// <summary>Represents how entity discrimination is enforced for DynamoDB shared-table mapping.</summary>
public enum DynamoDiscriminatorStrategy
{
    /// <summary>
    ///     Uses a dedicated discriminator attribute (for example, <c>$type</c>) to distinguish entity
    ///     types.
    /// </summary>
    Attribute = 0,
}
