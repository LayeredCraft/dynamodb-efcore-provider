using System.Linq.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Base visitor for SQL expression trees.
/// </summary>
public abstract class SqlExpressionVisitor : ExpressionVisitor
{
    /// <inheritdoc />
    protected override Expression VisitExtension(Expression node)
        => node switch
        {
            SqlBinaryExpression sqlBinaryExpression => VisitSqlBinary(sqlBinaryExpression),
            SqlConstantExpression sqlConstantExpression => VisitSqlConstant(sqlConstantExpression),
            SqlParameterExpression sqlParameterExpression => VisitSqlParameter(
                sqlParameterExpression),
            SqlPropertyExpression sqlPropertyExpression => VisitSqlProperty(sqlPropertyExpression),
            ProjectionExpression projectionExpression => VisitProjection(projectionExpression),
            SelectExpression selectExpression => VisitSelect(selectExpression),
            _ => base.VisitExtension(node),
        };

    /// <summary>
    /// Visits a SQL binary expression.
    /// </summary>
    protected abstract Expression VisitSqlBinary(SqlBinaryExpression sqlBinaryExpression);

    /// <summary>
    /// Visits a SQL constant expression.
    /// </summary>
    protected abstract Expression VisitSqlConstant(SqlConstantExpression sqlConstantExpression);

    /// <summary>
    /// Visits a SQL parameter expression.
    /// </summary>
    protected abstract Expression VisitSqlParameter(SqlParameterExpression sqlParameterExpression);

    /// <summary>
    /// Visits a SQL property expression.
    /// </summary>
    protected abstract Expression VisitSqlProperty(SqlPropertyExpression sqlPropertyExpression);

    /// <summary>
    /// Visits a projection expression.
    /// </summary>
    protected abstract Expression VisitProjection(ProjectionExpression projectionExpression);

    /// <summary>
    /// Visits a SELECT expression.
    /// </summary>
    protected abstract Expression VisitSelect(SelectExpression selectExpression);
}
