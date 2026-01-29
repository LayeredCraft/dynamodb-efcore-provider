using System.Linq.Expressions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
/// Represents a single projected column in a SELECT statement.
/// </summary>
public class ProjectionExpression(SqlExpression expression, string alias) : Expression
{
    /// <summary>
    /// The SQL expression being projected.
    /// </summary>
    public SqlExpression Expression { get; } = expression;

    /// <summary>
    /// The alias for this projection (optional, can be empty).
    /// </summary>
    public string Alias { get; } = alias;

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    public override Type Type => Expression.Type;

    /// <summary>
    /// Creates a new projection with an updated expression.
    /// </summary>
    public ProjectionExpression Update(SqlExpression expression)
        => expression == Expression ? this : new ProjectionExpression(expression, Alias);

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
        => Update((SqlExpression)visitor.Visit(Expression));

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is ProjectionExpression other
           && Expression.Equals(other.Expression)
           && Alias == other.Alias;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Expression, Alias);
}
