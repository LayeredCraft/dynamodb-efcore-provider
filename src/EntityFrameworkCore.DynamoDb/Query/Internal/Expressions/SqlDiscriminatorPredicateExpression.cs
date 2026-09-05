using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
///     Wraps a discriminator predicate so it can be identified and treated separately from other
///     query predicates during analysis and validation.
/// </summary>
/// <remarks>
///     The predicate can be injected for shared-table (TPH) root materialization or translated from
///     a LINQ type filter (for example, <c>entity is Order</c>). Wrapping it lets validators,
///     analyzers, and normalization distinguish it from ordinary filters without hiding it from tree
///     walkers. Future analysis passes can inspect, augment, or replace it by
///     pattern-matching on <see cref="SqlDiscriminatorPredicateExpression" />.
/// </remarks>
public sealed class SqlDiscriminatorPredicateExpression(
    SqlExpression predicate,
    string? discriminatorAttributeName = null,
    DiscriminatorPredicateOrigin origin = DiscriminatorPredicateOrigin.Explicit,
    CoreTypeMapping? typeMapping = null) : SqlExpression(
    predicate.Type,
    typeMapping ?? predicate.TypeMapping)
{
    /// <summary>The underlying discriminator filter expression.</summary>
    public SqlExpression Predicate { get; } = predicate;

    /// <summary>The mapped discriminator attribute, when the predicate filters a hierarchy.</summary>
    public string? DiscriminatorAttributeName { get; } = discriminatorAttributeName;

    /// <summary>Whether this predicate comes from root materialization or an explicit type filter.</summary>
    public DiscriminatorPredicateOrigin Origin { get; } = origin;

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Append("[discriminator: ");
        expressionPrinter.Visit(Predicate);
        expressionPrinter.Append("]");
    }

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        => new SqlDiscriminatorPredicateExpression(
            Predicate,
            DiscriminatorAttributeName,
            Origin,
            typeMapping);

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is SqlDiscriminatorPredicateExpression disc
            && base.Equals(disc)
            && Predicate.Equals(disc.Predicate);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Predicate);
}

/// <summary>Identifies how a discriminator predicate entered a query.</summary>
public enum DiscriminatorPredicateOrigin
{
    /// <summary>The predicate was translated from an explicit LINQ type filter.</summary>
    Explicit,

    /// <summary>The predicate was injected to limit a root materializer to its hierarchy.</summary>
    RootMaterializer
}
