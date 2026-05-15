namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public static class SkipReason
{
    public const string NullableKeysNotSupported = "DynamoDB does not support nullable keys.";
    public const string ShadowKeysNotSupported = "DynamoDB does not support shadow keys.";
    public const string ForeignKeysNotSupported = "DynamoDB does not support foreign keys.";

    public const string NavigationPropertiesNotSupported =
        "DynamoDB does not support navigation properties.";

    public const string ArrayComplexCollectionsNotSupported =
        "DynamoDB complex collections currently support List<T> and IList<T>, not array-backed collections.";

    public const string ComplexCollectionScanMaterializationNotSupported =
        "DynamoDB provider currently cannot materialize every complex collection shape from scan-like queries used by this state-change specification test.";
}
