using System.Linq.Expressions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Provider-specific translation messages for unsupported LINQ query shapes.</summary>
internal static class DynamoStrings
{
    public const string AggregatesUnavailableReason =
        "DynamoDB PartiQL supports SIZE(path) but does not support query aggregates such as COUNT, SUM, AVG, MIN, or MAX.";

    public const string SetOperationsNotSupported =
        "DynamoDB PartiQL does not support LINQ set operations (Union/Concat/Except/Intersect).";

    public const string JoinsNotSupported =
        "DynamoDB PartiQL does not support JOIN-style query operators.";

    public const string GroupByNotSupported = "DynamoDB PartiQL does not support GROUP BY.";

    public const string DistinctNotSupported = "DynamoDB PartiQL does not support SELECT DISTINCT.";

    public const string OffsetOperatorsNotSupported =
        "DynamoDB PartiQL does not support OFFSET-based operators such as Skip and ElementAt.";

    public const string ReverseNotSupported =
        "DynamoDB PartiQL does not support reversing query results without explicit server-side support.";

    public const string CastNotSupported =
        "DynamoDB PartiQL does not support CAST in query translation.";

    public const string LastNotSupported =
        "DynamoDB queries do not support Last/LastOrDefault translation without offset semantics.";

    public const string SkipWhileNotSupported =
        "DynamoDB PartiQL does not support SkipWhile translation.";

    public const string TakeWhileNotSupported =
        "DynamoDB PartiQL does not support TakeWhile translation.";

    public const string ContainsNotSupportedYet =
        "Contains translation is not currently supported by this provider.";

    public const string OfTypeNotSupportedYet =
        "OfType translation is not currently supported by this provider.";

    public const string PredicateNotTranslatable =
        "The predicate could not be translated to DynamoDB PartiQL.";

    public const string MethodCallInPredicateNotSupported =
        "Method calls are not supported in server-side DynamoDB predicate translation.";

    public const string StringContainsOverloadNotSupported =
        "Only string.Contains(string) is supported in server-side DynamoDB predicate translation.";

    public const string ContainsCollectionShapeNotSupported =
        "Contains translation supports in-memory collection membership only (for example, ids.Contains(entity.Id)).";

    public const string ContainsCollectionParameterMustBeEnumerable =
        "Contains translation requires the collection parameter to implement IEnumerable.";

    public const string MemberAccessNotSupported =
        "Only direct entity member access is supported for server-side DynamoDB translation.";

    public const string UnaryOperatorNotSupported =
        "Only simple type conversions are supported in server-side DynamoDB translation.";

    public static string UnsupportedOperator(string operatorName, string reason)
        => $"The LINQ operator '{operatorName}' is not supported by the DynamoDB provider. {reason}";

    public static string ProviderOperatorNotSupportedYet(string operatorName)
        => $"The LINQ operator '{operatorName}' is not currently supported by this provider.";

    public static string AggregatesNotSupported(string operatorName)
        => $"The LINQ operator '{operatorName}' is not supported by the DynamoDB provider. {AggregatesUnavailableReason}";

    public static string UnsupportedBinaryOperator(ExpressionType operatorType)
        => $"Binary operator '{operatorType}' is not supported by server-side DynamoDB translation.";

    public static string InListTooLarge(int maxValues, bool isPartitionKeyComparison)
        => isPartitionKeyComparison
            ? $"Contains translation exceeded DynamoDB IN limit of {maxValues} values for partition key comparisons."
            : $"Contains translation exceeded DynamoDB IN limit of {maxValues} values for non-key comparisons.";
}
