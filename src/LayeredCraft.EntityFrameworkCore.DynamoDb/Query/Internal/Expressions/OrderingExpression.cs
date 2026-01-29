namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
/// Represents an ORDER BY clause component.
/// </summary>
public class OrderingExpression(SqlExpression expression, bool isAscending)
{
    /// <summary>
    /// The expression to order by.
    /// </summary>
    public SqlExpression Expression { get; } = expression;

    /// <summary>
    /// Whether the ordering is ascending (true) or descending (false).
    /// </summary>
    public bool IsAscending { get; } = isAscending;

    /// <summary>
    /// Creates a new ordering expression with an updated expression.
    /// </summary>
    public OrderingExpression Update(SqlExpression expression)
        => !Equals(expression, Expression) ? new OrderingExpression(expression, IsAscending) : this;

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is OrderingExpression other
           && Expression.Equals(other.Expression)
           && IsAscending == other.IsAscending;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Expression, IsAscending);
}
