using System.Linq.Expressions;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Provider-specific translation messages for unsupported LINQ query shapes.</summary>
internal static class DynamoStrings
{
    /// <summary>Reason fragment appended to aggregate-operator error messages.</summary>
    public const string AggregatesUnavailableReason =
        "DynamoDB PartiQL supports SIZE(path) but does not support query aggregates such as COUNT, SUM, AVG, MIN, or MAX.";

    /// <summary>Error message for unsupported set operations (Union/Concat/Except/Intersect).</summary>
    public const string SetOperationsNotSupported =
        "DynamoDB PartiQL does not support LINQ set operations (Union/Concat/Except/Intersect).";

    /// <summary>Error message for unsupported JOIN operators.</summary>
    public const string JoinsNotSupported =
        "DynamoDB PartiQL does not support JOIN-style query operators.";

    /// <summary>Error message for unsupported GroupBy.</summary>
    public const string GroupByNotSupported = "DynamoDB PartiQL does not support GROUP BY.";

    /// <summary>Error message for unsupported Distinct.</summary>
    public const string DistinctNotSupported = "DynamoDB PartiQL does not support SELECT DISTINCT.";

    /// <summary>Error message for unsupported offset operators (Skip/ElementAt).</summary>
    public const string OffsetOperatorsNotSupported =
        "DynamoDB PartiQL does not support OFFSET-based operators such as Skip and ElementAt.";

    /// <summary>Error message for unsupported Reverse.</summary>
    public const string ReverseNotSupported =
        "DynamoDB PartiQL does not support reversing query results without explicit server-side support.";

    /// <summary>Error message for unsupported CAST in query translation.</summary>
    public const string CastNotSupported =
        "DynamoDB PartiQL does not support CAST in query translation.";

    /// <summary>Error message for unsupported Last/LastOrDefault.</summary>
    public const string LastNotSupported =
        "Last/LastOrDefault is not supported in this iteration. Reverse traversal "
        + "(equivalent to ScanIndexForward=false) is deferred to a future release.";

    /// <summary>Error message for the unsupported Take operator; directs users to Limit instead.</summary>
    public const string TakeNotSupported =
        "Take is not supported by this provider. Limit(n) is an evaluation budget (not a "
        + "row-count operator): it evaluates n items, applies filters, and returns 0..n results in a "
        + "single request. Use it only when evaluation-budget semantics are desired. "
        + "Example: .Limit(25).ToListAsync()";

    /// <summary>
    ///     Thrown when <c>First*</c> is used on a non-key or scan-like path. Server-side
    ///     <c>First*</c> is restricted to key-only queries; use <c>AsAsyncEnumerable()</c> for unsafe
    ///     paths.
    /// </summary>
    public static string FirstOrDefaultRequiresKeyOnlyPath(string queryShape)
        => $"First/FirstOrDefault on a {queryShape} is not supported server-side. "
            + "Server-side First* is restricted to key-only queries (partition-key equality with only "
            + "key predicates). For non-key filters or scan-like paths, fetch server-side then select "
            + "client-side via AsAsyncEnumerable(): "
            + ".Where(...).AsAsyncEnumerable().FirstOrDefaultAsync(ct)";

    /// <summary>
    ///     Thrown when <c>Limit(n)</c> and <c>First*</c> are combined directly. Use
    ///     <c>AsAsyncEnumerable()</c> to make the client-side selection explicit.
    /// </summary>
    public const string FirstOrDefaultWithUserLimitNotSupported =
        "Limit(n) cannot be combined with First/FirstOrDefault directly. "
        + "Limit(n) sets a DynamoDB evaluation budget and may return multiple items; "
        + "combining it with First* is ambiguous. Use AsAsyncEnumerable() to explicitly "
        + "take the first item client-side: "
        + ".Where(...).Limit(n).AsAsyncEnumerable().FirstOrDefaultAsync(ct)";

    /// <summary>Error message for unsupported SkipWhile translation.</summary>
    public const string SkipWhileNotSupported =
        "DynamoDB PartiQL does not support SkipWhile translation.";

    /// <summary>Error message for unsupported TakeWhile translation.</summary>
    public const string TakeWhileNotSupported =
        "DynamoDB PartiQL does not support TakeWhile translation.";

    /// <summary>Error message for Contains translation not yet supported.</summary>
    public const string ContainsNotSupportedYet =
        "Contains translation is not currently supported by this provider.";

    /// <summary>Error message for OfType translation not yet supported.</summary>
    public const string OfTypeNotSupportedYet =
        "OfType translation is not currently supported by this provider.";

    /// <summary>Error message for predicates that cannot be translated to PartiQL.</summary>
    public const string PredicateNotTranslatable =
        "The predicate could not be translated to DynamoDB PartiQL.";

    /// <summary>Error message for method calls in server-side predicate translation.</summary>
    public const string MethodCallInPredicateNotSupported =
        "Method calls are not supported in server-side DynamoDB predicate translation.";

    /// <summary>Error message for unsupported <c>string.Contains</c> overloads.</summary>
    public const string StringContainsOverloadNotSupported =
        "Only string.Contains(string) is supported in server-side DynamoDB predicate translation; char and StringComparison overloads are not translated.";

    /// <summary>Error message for unsupported <c>string.StartsWith</c> overloads.</summary>
    public const string StringStartsWithOverloadNotSupported =
        "Only string.StartsWith(string) is supported in server-side DynamoDB predicate translation; char, StringComparison, and culture/ignore-case overloads are not translated.";

    /// <summary>Error message for Contains with unsupported collection shapes.</summary>
    public const string ContainsCollectionShapeNotSupported =
        "Contains translation supports in-memory collection membership only (for example, ids.Contains(entity.Id)).";

    /// <summary>Error message for Contains when the collection parameter does not implement IEnumerable.</summary>
    public const string ContainsCollectionParameterMustBeEnumerable =
        "Contains translation requires the collection parameter to implement IEnumerable.";

    /// <summary>Error message for unsupported member access patterns in server-side translation.</summary>
    public const string MemberAccessNotSupported =
        "Only entity scalar property access and complex-type nested member paths are supported for server-side DynamoDB translation.";

    /// <summary>Error message for non-constant list index access in server-side translation.</summary>
    public const string ListIndexMustBeConstant =
        "List element access in server-side DynamoDB translation requires a constant integer index.";

    /// <summary>Error message for unsupported unary operators in translation.</summary>
    public const string UnaryOperatorNotSupported =
        "Only simple type conversions and logical negation of boolean predicates are supported in server-side DynamoDB translation.";

    /// <summary>Error message for unsupported bitwise complement; only logical negation is translated.</summary>
    public const string BitwiseComplementNotSupported =
        "Only logical negation of boolean search conditions is supported; bitwise complement is not translated.";

    /// <summary>Formats an error message for an unsupported LINQ operator with a provider-specific reason.</summary>
    public static string UnsupportedOperator(string operatorName, string reason)
        => $"The LINQ operator '{operatorName}' is not supported by the DynamoDB provider. {reason}";

    /// <summary>Formats an error message for a LINQ operator not yet implemented by this provider.</summary>
    public static string ProviderOperatorNotSupportedYet(string operatorName)
        => $"The LINQ operator '{operatorName}' is not currently supported by this provider.";

    /// <summary>Formats an error message for an unsupported aggregate operator.</summary>
    public static string AggregatesNotSupported(string operatorName)
        => $"The LINQ operator '{operatorName}' is not supported by the DynamoDB provider. {AggregatesUnavailableReason}";

    /// <summary>Formats an error message for an unsupported binary operator in server-side translation.</summary>
    public static string UnsupportedBinaryOperator(ExpressionType operatorType)
        => $"Binary operator '{operatorType}' is not supported by server-side DynamoDB translation.";

    /// <summary>
    ///     Formats an error message when a Contains translation exceeds the DynamoDB IN-list size
    ///     limit.
    /// </summary>
    public static string InListTooLarge(int maxValues, bool isPartitionKeyComparison)
        => isPartitionKeyComparison
            ? $"Contains translation exceeded DynamoDB IN limit of {maxValues} values for partition key comparisons."
            : $"Contains translation exceeded DynamoDB IN limit of {maxValues} values for non-key comparisons.";
}
