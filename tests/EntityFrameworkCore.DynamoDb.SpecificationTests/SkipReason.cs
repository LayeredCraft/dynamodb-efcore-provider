namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public static class SkipReason
{
    public const string NullableKeysNotSupported = "DynamoDB does not support nullable keys.";
    public const string ShadowKeysNotSupported = "DynamoDB does not support shadow keys.";
    public const string ForeignKeysNotSupported = "DynamoDB does not support foreign keys.";

    public const string QueryShapeNotSupported =
        "DynamoDB provider does not support this query shape.";

    public const string NestedStructComplexTypeProjectionNotSupported =
        "DynamoDB provider cannot materialize nested struct complex type projections due to the value-type expression tree shape.";

    public const string ComplexTypeConstantOrParameterEqualityNotSupported =
        "DynamoDB provider supports complex type property-to-property equality but not equality against complex constants or parameters.";

    public const string ComplexTypeSubqueriesNotSupported =
        "DynamoDB provider does not support complex type values in subqueries or Contains predicates.";

    public const string SubqueryPushdownNotSupported =
        "DynamoDB provider does not support the subquery pushdown required by this query shape.";

    public const string OrderedResultSetNotSupported =
        "DynamoDB does not support guaranteed ordered result sets for this query shape.";

    public const string NavigationPropertiesNotSupported =
        "DynamoDB does not support navigation properties.";

    public const string JoinsNotSupported =
        "DynamoDB PartiQL does not support joins.";

    public const string SetOperationsNotSupported =
        "DynamoDB PartiQL does not support set operations.";

    public const string GroupByNotSupported =
        "DynamoDB PartiQL does not support GROUP BY.";

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
