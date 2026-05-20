namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public static class SkipReason
{
    public const string NullableKeysNotSupported = "DynamoDB does not support nullable keys.";
    public const string ShadowKeysNotSupported = "DynamoDB does not support shadow keys.";
    public const string ForeignKeysNotSupported = "DynamoDB does not support foreign keys.";

    public const string QueryShapeNotSupported =
        "DynamoDB provider does not support this query shape.";

    public const string OrderedResultSetNotSupported =
        "DynamoDB does not support guaranteed ordered result sets for this query shape.";

    public const string NavigationPropertiesNotSupported =
        "DynamoDB does not support navigation properties.";

    public const string TransactionsNotSupported =
        "DynamoDB provider does not support explicit EF Core transaction scopes via Database.BeginTransaction().";

    public const string ComplexTypeCollectionsNotSupported =
        "DynamoDB provider does not support complex type collections for this test shape.";

}
