using System.Linq.Expressions;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Provider-specific translation messages for unsupported LINQ query shapes.</summary>
internal static class DynamoStrings
{
    /// <summary>Provides functionality for this member.</summary>
    public const string AggregatesUnavailableReason =
        "DynamoDB PartiQL supports SIZE(path) but does not support query aggregates such as COUNT, SUM, AVG, MIN, or MAX.";

    /// <summary>Provides functionality for this member.</summary>
    public const string SetOperationsNotSupported =
        "DynamoDB PartiQL does not support LINQ set operations (Union/Concat/Except/Intersect).";

    /// <summary>Provides functionality for this member.</summary>
    public const string JoinsNotSupported =
        "DynamoDB PartiQL does not support JOIN-style query operators.";

    /// <summary>Provides functionality for this member.</summary>
    public const string GroupByNotSupported = "DynamoDB PartiQL does not support GROUP BY.";

    /// <summary>Provides functionality for this member.</summary>
    public const string DistinctNotSupported = "DynamoDB PartiQL does not support SELECT DISTINCT.";

    /// <summary>Provides functionality for this member.</summary>
    public const string OffsetOperatorsNotSupported =
        "DynamoDB PartiQL does not support OFFSET-based operators such as Skip and ElementAt.";

    /// <summary>Provides functionality for this member.</summary>
    public const string ReverseNotSupported =
        "DynamoDB PartiQL does not support reversing query results without explicit server-side support.";

    /// <summary>Provides functionality for this member.</summary>
    public const string CastNotSupported =
        "DynamoDB PartiQL does not support CAST in query translation.";

    /// <summary>Provides functionality for this member.</summary>
    public const string LastNotSupported =
        "DynamoDB queries do not support Last/LastOrDefault translation without offset semantics.";

    /// <summary>Provides functionality for this member.</summary>
    public const string SkipWhileNotSupported =
        "DynamoDB PartiQL does not support SkipWhile translation.";

    /// <summary>Provides functionality for this member.</summary>
    public const string TakeWhileNotSupported =
        "DynamoDB PartiQL does not support TakeWhile translation.";

    /// <summary>Provides functionality for this member.</summary>
    public const string ContainsNotSupportedYet =
        "Contains translation is not currently supported by this provider.";

    /// <summary>Provides functionality for this member.</summary>
    public const string OfTypeNotSupportedYet =
        "OfType translation is not currently supported by this provider.";

    /// <summary>Provides functionality for this member.</summary>
    public const string PredicateNotTranslatable =
        "The predicate could not be translated to DynamoDB PartiQL.";

    /// <summary>Provides functionality for this member.</summary>
    public const string MethodCallInPredicateNotSupported =
        "Method calls are not supported in server-side DynamoDB predicate translation.";

    /// <summary>Provides functionality for this member.</summary>
    public const string StringContainsOverloadNotSupported =
        "Only string.Contains(string) is supported in server-side DynamoDB predicate translation; char and StringComparison overloads are not translated.";

    /// <summary>Provides functionality for this member.</summary>
    public const string StringStartsWithOverloadNotSupported =
        "Only string.StartsWith(string) is supported in server-side DynamoDB predicate translation; char, StringComparison, and culture/ignore-case overloads are not translated.";

    /// <summary>Provides functionality for this member.</summary>
    public const string ContainsCollectionShapeNotSupported =
        "Contains translation supports in-memory collection membership only (for example, ids.Contains(entity.Id)).";

    /// <summary>Provides functionality for this member.</summary>
    public const string ContainsCollectionParameterMustBeEnumerable =
        "Contains translation requires the collection parameter to implement IEnumerable.";

    /// <summary>Provides functionality for this member.</summary>
    public const string MemberAccessNotSupported =
        "Only entity scalar property access and owned-type nested member paths are supported for server-side DynamoDB translation.";

    /// <summary>Provides functionality for this member.</summary>
    public const string ListIndexMustBeConstant =
        "List element access in server-side DynamoDB translation requires a constant integer index.";

    /// <summary>Provides functionality for this member.</summary>
    public const string UnaryOperatorNotSupported =
        "Only simple type conversions and logical negation of boolean predicates are supported in server-side DynamoDB translation.";

    /// <summary>Provides functionality for this member.</summary>
    public const string BitwiseComplementNotSupported =
        "Only logical negation of boolean search conditions is supported; bitwise complement is not translated.";

    /// <summary>Provides functionality for this member.</summary>
    public static string UnsupportedOperator(string operatorName, string reason)
        => $"The LINQ operator '{operatorName}' is not supported by the DynamoDB provider. {reason}";

    /// <summary>Provides functionality for this member.</summary>
    public static string ProviderOperatorNotSupportedYet(string operatorName)
        => $"The LINQ operator '{operatorName}' is not currently supported by this provider.";

    /// <summary>Provides functionality for this member.</summary>
    public static string AggregatesNotSupported(string operatorName)
        => $"The LINQ operator '{operatorName}' is not supported by the DynamoDB provider. {AggregatesUnavailableReason}";

    /// <summary>Provides functionality for this member.</summary>
    public static string UnsupportedBinaryOperator(ExpressionType operatorType)
        => $"Binary operator '{operatorType}' is not supported by server-side DynamoDB translation.";

    /// <summary>Provides functionality for this member.</summary>
    public static string InListTooLarge(int maxValues, bool isPartitionKeyComparison)
        => isPartitionKeyComparison
            ? $"Contains translation exceeded DynamoDB IN limit of {maxValues} values for partition key comparisons."
            : $"Contains translation exceeded DynamoDB IN limit of {maxValues} values for non-key comparisons.";
}
