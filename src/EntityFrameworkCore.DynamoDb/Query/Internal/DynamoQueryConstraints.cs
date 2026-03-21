using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>The key-condition operator applied to a sort-key in a DynamoDB key-condition query.</summary>
internal enum SkOperator
{
    Equal,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    Between,
    BeginsWith,
}

/// <summary>A single key-condition constraint on a sort-key property.</summary>
/// Primary value: equality target, range bound, or <c>begins_with</c> prefix.
/// Upper bound for <c>SkOperator.Between</c> is held in <paramref name="High"/>.
internal sealed record SkConstraint(
    SkOperator Operator,
    SqlExpression Low,
    SqlExpression? High = null);

/// <summary>
/// Extracted key-condition constraints from a finalized <c>SelectExpression</c> predicate.
/// Produced by <c>DynamoConstraintExtractionVisitor</c>; consumed by
/// <c>IDynamoIndexSelectionAnalyzer</c>.
/// </summary>
internal sealed record DynamoQueryConstraints(
    IReadOnlyDictionary<string, SqlExpression> EqualityConstraints,
    IReadOnlyDictionary<string, SkConstraint> SkKeyConditions,
    bool HasUnsafeOr,
    IReadOnlySet<string> OrderingPropertyNames);
