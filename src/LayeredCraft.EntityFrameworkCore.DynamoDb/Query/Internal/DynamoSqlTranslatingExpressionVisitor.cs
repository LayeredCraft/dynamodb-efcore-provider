using System.Linq.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Translates C# expression trees to SQL expression trees.
/// </summary>
public class DynamoSqlTranslatingExpressionVisitor(ISqlExpressionFactory sqlExpressionFactory)
    : ExpressionVisitor
{
    /// <summary>
    /// Translates a C# expression to a SQL expression.
    /// </summary>
    public SqlExpression? Translate(Expression expression)
    {
        var result = Visit(expression);
        return result as SqlExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitBinary(BinaryExpression node)
    {
        var left = Translate(node.Left);
        var right = Translate(node.Right);

        if (left == null || right == null)
            return node;

        return sqlExpressionFactory.Binary(node.NodeType, left, right)
            ?? throw new InvalidOperationException(
                $"Binary operator {node.NodeType} is not supported");
    }

    /// <inheritdoc />
    protected override Expression VisitConstant(ConstantExpression node)
        => sqlExpressionFactory.Constant(node.Value, node.Type);

    /// <inheritdoc />
    protected override Expression VisitParameter(ParameterExpression node)
        => QueryCompilationContext.NotTranslatedExpression;

    /// <inheritdoc />
    protected override Expression VisitMember(MemberExpression node)
    {
        // Handle property access on the entity parameter (e.g., item.IntValue)
        if (node.Expression is ParameterExpression)
            return sqlExpressionFactory.Property(node.Member.Name, node.Type);

        // For non-parameter member access, don't recurse with base visitor because EF may
        // surface object-typed query expressions here, and ExpressionVisitor reconstruction
        // can throw when re-binding members on System.Object.
        // Let the projection pipeline fall back to index-based client projection.
        return QueryCompilationContext.NotTranslatedExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitExtension(Expression extensionExpression)
    {
        switch (extensionExpression)
        {
            case SqlExpression sqlExpression:
                return sqlExpression;

            case QueryParameterExpression queryParameter:
                // Captured variables become parameters (runtime lookup)
                return sqlExpressionFactory.Parameter(queryParameter.Name, queryParameter.Type);

            default:
                return QueryCompilationContext.NotTranslatedExpression;
        }
    }

    /// <inheritdoc />
    protected override Expression VisitMethodCall(MethodCallExpression node)
        => QueryCompilationContext.NotTranslatedExpression;

    /// <inheritdoc />
    protected override Expression VisitUnary(UnaryExpression node)
    {
        // Handle conversions and other unary operations
        if (node.NodeType == ExpressionType.Convert
            || node.NodeType == ExpressionType.ConvertChecked)
        {
            var operand = Visit(node.Operand);
            if (operand == QueryCompilationContext.NotTranslatedExpression)
                return QueryCompilationContext.NotTranslatedExpression;

            if (operand is SqlExpression sqlOperand)
                // Apply type mapping for the conversion
                return sqlExpressionFactory.ApplyTypeMapping(sqlOperand, node.Type);

            return QueryCompilationContext.NotTranslatedExpression;
        }

        return QueryCompilationContext.NotTranslatedExpression;
    }
}
