namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public static class SkipReason
{
    public const string NullableKeysNotSupported = "DynamoDB does not support nullable keys.";
    public const string ShadowKeysNotSupported = "DynamoDB does not support shadow keys.";
    public const string ForeignKeysNotSupported = "DynamoDB does not support foreign keys.";

    public const string QueryShapeNotSupported =
        "DynamoDB provider does not support this query shape.";

    public const string SubqueryContainsNotSupported =
        "DynamoDB provider does not support translating subquery Contains.";

    public const string OrderedResultSetNotSupported =
        "DynamoDB does not support guaranteed ordered result sets for this query shape.";

    public const string NavigationPropertiesNotSupported =
        "DynamoDB does not support navigation properties.";

    public const string JoinsNotSupported =
        "DynamoDB PartiQL does not support joins.";

    public const string EntityTypeNotMappedInFixture =
        "This entity type is not mapped in the DynamoDB specification test fixture.";

    public const string ThreePartCompositeKeysNotSupported =
        "DynamoDB table keys support only a partition key and optional sort key.";

    public const string TransactionsNotSupported =
        "DynamoDB provider does not support explicit EF Core transaction scopes via Database.BeginTransaction().";

    public const string OwnedEntityTypesNotSupported =
        "DynamoDB does not support EF Core owned entity types (OwnsMany/OwnsOne). Use [ComplexType] instead.";

    public const string PartitionKeyRequiredOnAllEntities =
        "DynamoDB requires every entity type in the model to have a partition key.";

}
