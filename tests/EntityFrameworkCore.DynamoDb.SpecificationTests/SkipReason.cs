namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public static class SkipReason
{
    public const string NullableKeysNotSupported = "DynamoDB does not support nullable keys.";
    public const string ShadowKeysNotSupported = "DynamoDB does not support shadow keys.";
    public const string ForeignKeysNotSupported = "DynamoDB does not support foreign keys.";

    public const string QueryShapeNotSupported =
        "DynamoDB provider does not support this query shape.";

    public const string NavigationPropertiesNotSupported =
        "DynamoDB does not support navigation properties.";
}
