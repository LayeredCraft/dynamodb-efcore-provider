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
            SqlUnaryExpression sqlUnaryExpression => VisitSqlUnary(sqlUnaryExpression),
            SqlParenthesizedExpression sqlParenthesizedExpression => VisitSqlParenthesized(
                sqlParenthesizedExpression),
            SqlConstantExpression sqlConstantExpression => VisitSqlConstant(sqlConstantExpression),
            SqlParameterExpression sqlParameterExpression => VisitSqlParameter(
                sqlParameterExpression),
            SqlPropertyExpression sqlPropertyExpression => VisitSqlProperty(sqlPropertyExpression),
            SqlInExpression sqlInExpression => VisitSqlIn(sqlInExpression),
            SqlFunctionExpression sqlFunctionExpression => VisitSqlFunction(sqlFunctionExpression),
            SqlIsNullExpression sqlIsNullExpression => VisitSqlIsNull(sqlIsNullExpression),
            SqlBetweenExpression sqlBetweenExpression => VisitSqlBetween(sqlBetweenExpression),
            ProjectionExpression projectionExpression => VisitProjection(projectionExpression),
            SelectExpression selectExpression => VisitSelect(selectExpression),
            DynamoScalarAccessExpression scalarAccess => VisitDynamoScalarAccess(scalarAccess),
            DynamoListIndexExpression listIndex => VisitDynamoListIndex(listIndex),
            _ => base.VisitExtension(node),
        };

    /// <summary>
    /// Visits a SQL binary expression.
    /// </summary>
    protected abstract Expression VisitSqlBinary(SqlBinaryExpression sqlBinaryExpression);

    /// <summary>Visits a SQL unary expression.</summary>
    protected abstract Expression VisitSqlUnary(SqlUnaryExpression sqlUnaryExpression);

    /// <summary>Visits a SQL parenthesized expression.</summary>
    protected abstract Expression VisitSqlParenthesized(
        SqlParenthesizedExpression sqlParenthesizedExpression);

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

    /// <summary>Visits a SQL IN expression.</summary>
    protected abstract Expression VisitSqlIn(SqlInExpression sqlInExpression);

    /// <summary>Visits a SQL function expression.</summary>
    protected abstract Expression VisitSqlFunction(SqlFunctionExpression sqlFunctionExpression);

    /// <summary>Visits a SQL IS NULL / IS MISSING expression.</summary>
    protected abstract Expression VisitSqlIsNull(SqlIsNullExpression sqlIsNullExpression);

    /// <summary>Visits a SQL BETWEEN range predicate expression.</summary>
    protected abstract Expression VisitSqlBetween(SqlBetweenExpression sqlBetweenExpression);

    /// <summary>
    /// Visits a projection expression.
    /// </summary>
    protected abstract Expression VisitProjection(ProjectionExpression projectionExpression);

    /// <summary>
    /// Visits a SELECT expression.
    /// </summary>
    protected abstract Expression VisitSelect(SelectExpression selectExpression);

    /// <summary>Visits a nested scalar document path access expression.</summary>
    protected abstract Expression VisitDynamoScalarAccess(
        DynamoScalarAccessExpression scalarAccessExpression);

    /// <summary>Visits a list element index access expression.</summary>
    protected abstract Expression VisitDynamoListIndex(
        DynamoListIndexExpression listIndexExpression);
}
