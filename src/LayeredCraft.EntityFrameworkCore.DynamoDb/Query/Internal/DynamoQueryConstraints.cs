using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

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
/// <param name="Operator">The key-condition operator for this sort-key property.</param>
/// <param name="Low">
/// Primary value: equality target, range bound, or <c>begins_with</c> prefix.
/// Upper bound for <see cref="SkOperator.Between"/> is held in <paramref name="High"/>.
/// </param>
/// <param name="High">Upper bound for <see cref="SkOperator.Between"/>; <c>null</c> for all other operators.</param>
internal sealed record SkConstraint(SkOperator Operator, SqlExpression Low, SqlExpression? High = null);

/// <summary>
/// Extracted key-condition constraints from a finalized <see cref="SelectExpression"/> predicate.
/// Produced by <see cref="DynamoConstraintExtractionVisitor"/>; consumed by
/// <see cref="IDynamoIndexSelectionAnalyzer"/>.
/// </summary>
internal sealed record DynamoQueryConstraints(
    IReadOnlyDictionary<string, SqlExpression> EqualityConstraints,
    IReadOnlyDictionary<string, IReadOnlyList<SqlExpression>> InConstraints,
    IReadOnlyDictionary<string, SkConstraint> SkKeyConditions,
    bool HasUnsafeOr,
    IReadOnlySet<string> OrderingPropertyNames);
