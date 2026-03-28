using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
///     Wraps a provider-injected discriminator predicate so it can be identified and treated
///     separately from user-supplied predicates during analysis and validation.
/// </summary>
/// <remarks>
///     The discriminator predicate is automatically injected for shared-table (TPH) inheritance
///     queries (e.g. <c>Discriminator = 'Order'</c>). Wrapping it in this expression type allows
///     validators and analyzers to distinguish it from user-supplied non-key filters without hiding it
///     from tree walkers — any visitor that traverses the predicate tree encounters it as a typed,
///     identifiable node. Future analysis passes can inspect, augment, or replace it by
///     pattern-matching on <see cref="SqlDiscriminatorPredicateExpression" />.
/// </remarks>
public sealed class SqlDiscriminatorPredicateExpression(
    SqlExpression predicate,
    CoreTypeMapping? typeMapping = null) : SqlExpression(
    predicate.Type,
    typeMapping ?? predicate.TypeMapping)
{
    /// <summary>The underlying discriminator filter expression.</summary>
    public SqlExpression Predicate { get; } = predicate;

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Append("[discriminator: ");
        expressionPrinter.Visit(Predicate);
        expressionPrinter.Append("]");
    }

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        => new SqlDiscriminatorPredicateExpression(Predicate, typeMapping);

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is SqlDiscriminatorPredicateExpression disc
            && base.Equals(disc)
            && Predicate.Equals(disc.Predicate);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Predicate);
}
