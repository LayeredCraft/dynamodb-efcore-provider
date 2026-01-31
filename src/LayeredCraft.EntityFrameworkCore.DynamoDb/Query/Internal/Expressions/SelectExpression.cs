using System.Linq.Expressions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
/// Represents a SELECT query expression for DynamoDB PartiQL.
/// </summary>
public class SelectExpression(string tableName) : Expression
{
    private readonly List<OrderingExpression> _orderings = [];
    private readonly List<ProjectionExpression> _projection = [];

    /// <summary>
    /// The name of the DynamoDB table to query.
    /// </summary>
    public string TableName { get; } = tableName;

    /// <summary>
    /// The WHERE clause predicate, or null if no filtering is applied.
    /// </summary>
    public SqlExpression? Predicate { get; private set; }

    /// <summary>
    /// The ORDER BY clauses.
    /// </summary>
    public IReadOnlyList<OrderingExpression> Orderings => _orderings;

    /// <summary>
    /// The list of projected columns for the SELECT clause.
    /// Must have at least one projection - SELECT * is not supported.
    /// </summary>
    public IReadOnlyList<ProjectionExpression> Projection => _projection;

    /// <summary>
    /// Applies or combines a WHERE predicate.
    /// </summary>
    public void ApplyPredicate(SqlExpression predicate)
        => Predicate =
            Predicate == null
                ? predicate
                : new SqlBinaryExpression(
                    ExpressionType.AndAlso,
                    Predicate,
                    predicate,
                    typeof(bool),
                    null);

    /// <summary>
    /// Replaces all orderings with a single ordering.
    /// </summary>
    public void ApplyOrdering(OrderingExpression ordering)
    {
        _orderings.Clear();
        _orderings.Add(ordering);
    }

    /// <summary>
    /// Appends an additional ordering (for ThenBy).
    /// </summary>
    public void AppendOrdering(OrderingExpression ordering) => _orderings.Add(ordering);

    /// <summary>
    /// Adds a projection to the SELECT clause.
    /// </summary>
    public void AddToProjection(ProjectionExpression projectionExpression)
        => _projection.Add(projectionExpression);

    /// <summary>
    /// Clears all projections.
    /// </summary>
    public void ClearProjection() => _projection.Clear();

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    public override Type Type => typeof(object);

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
        =>
            // SelectExpression is immutable from the perspective of the visitor
            this;
}
