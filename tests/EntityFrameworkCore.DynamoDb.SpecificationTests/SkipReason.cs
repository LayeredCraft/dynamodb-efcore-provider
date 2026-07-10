namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public static class SkipReason
{
    public const string NullableKeysNotSupported = "DynamoDB does not support nullable keys.";
    public const string ShadowKeysNotSupported = "DynamoDB does not support shadow keys.";
    public const string ForeignKeysNotSupported = "DynamoDB does not support foreign keys.";

    public const string SharedDataTypesFixtureRequiresForeignKeys =
        "This shared EF Core data-types fixture requires foreign-key relationship mappings that DynamoDB does not support.";

    public const string QueryShapeNotSupported =
        "DynamoDB provider does not support this query shape.";

    public const string GuidNewGuidPredicateNotSupported =
        "DynamoDB provider does not translate Guid.NewGuid() in server-side predicates.";

    public const string CustomTypeEqualityIssue241 =
        "DynamoDB provider does not support this query shape. See issue #241.";

    public const string ConditionalBoolValueConversionIssue243 =
        "DynamoDB provider does not support this query shape. See issue #243.";

    public const string CaseInsensitiveStringComparisonNotSupported =
        "DynamoDB PartiQL does not support case-insensitive string comparisons.";

    public const string StringFunctionNotSupported =
        "DynamoDB PartiQL has no corresponding string function for this operation.";

    public const string SameAttributeStringFunctionOperandsNotSupported =
        "DynamoDB rejects string functions when both operands reference the same attribute.";

    public const string EndsWithNotSupported = "DynamoDB PartiQL has no ends_with function.";

    public const string BeginsWithRejectsNullArgument =
        "DynamoDB PartiQL begins_with rejects null arguments.";

    public const string StringAggregateNotSupported =
        "DynamoDB PartiQL has no GROUP BY or string aggregate support.";

    public const string RegexNotSupported = "DynamoDB PartiQL has no regex support.";

    public const string StringCompareZeroComparisonsOnly =
        "DynamoDB provider translates string.Compare/CompareTo only for comparisons against 0 because .NET only guarantees negative, zero, or positive results.";

    public const string ByteArrayElementAccessNotSupported =
        "DynamoDB PartiQL does not support indexing into binary attributes.";

    public const string ByteArrayContainsElementNotSupported =
        "DynamoDB PartiQL contains() supports set/list membership, but byte[] maps to a single binary attribute rather than a byte collection.";

    public const string TemporalFunctionNotSupported =
        "DynamoDB PartiQL has no corresponding temporal function for this operation.";

    public const string ComplexTypeSubqueriesNotSupported =
        "DynamoDB provider does not support complex type values in subqueries or Contains predicates.";

    public const string ComplexCollectionStructuralEqualityNotSupported =
        "DynamoDB provider does not support structural equality comparisons for complex collection properties. See issue #257.";

    public const string ComplexProjectionMaterializationGap =
        "DynamoDB provider does not yet support this complex-property projection materialization shape.";

    public const string SubqueryPushdownNotSupported =
        "DynamoDB provider does not support the subquery pushdown required by this query shape.";

    public const string SubqueryContainsNotSupported =
        "DynamoDB provider does not support translating subquery Contains.";

    public const string OrderedResultSetNotSupported =
        "DynamoDB does not support guaranteed ordered result sets for this query shape.";

    public const string SyncQueriesNotSupported =
        "DynamoDB provider supports async query execution only.";

    public const string SyncSaveChangesNotSupported =
        "DynamoDB provider supports async SaveChanges only.";

    public const string NavigationPropertiesNotSupported =
        "DynamoDB does not support navigation properties.";

    public const string JoinsNotSupported = "DynamoDB PartiQL does not support joins.";

    public const string SetOperationsNotSupported =
        "DynamoDB PartiQL does not support set operations.";

    public const string GroupByNotSupported = "DynamoDB PartiQL does not support GROUP BY.";

    public const string CountAggregatesNotSupported =
        "DynamoDB PartiQL does not support COUNT aggregates.";

    public const string PrimitiveCollectionQueryShapeNotSupported =
        "DynamoDB provider does not yet support this primitive collection query shape.";

    public const string OutOfBoundsListIndexReturnsNull =
        "DynamoDB returns NULL for out-of-bounds list index access; upstream test expects an exception.";

    public const string BitwiseEnumOperatorsNotSupported =
        "DynamoDB PartiQL does not support bitwise enum operators or Enum.HasFlag translation.";

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
