namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public static class SkipReason
{
    public const string NullableKeysNotSupported = "DynamoDB does not support nullable keys.";
    public const string ShadowKeysNotSupported = "DynamoDB does not support shadow keys.";
    public const string ForeignKeysNotSupported = "DynamoDB does not support foreign keys.";

    public const string NavigationPropertiesNotSupported =
        "DynamoDB does not support navigation properties.";

    public const string ProviderTypeConvertedPredicatesNotSupported =
        "Provider-type converted predicates need DynamoDB-specific support.";

    public const string BinaryProviderLiteralsNotSupported =
        "Binary provider literals need parameterization instead of inline PartiQL constants.";

    public const string ScalarEqualsTranslationNotSupported =
        "Scalar Equals method translation is not supported yet.";
}
