using System.Linq.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

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
        => sqlExpressionFactory.Parameter(node.Name ?? "p", node.Type);

    /// <inheritdoc />
    protected override Expression VisitMember(MemberExpression node)
    {
        // Handle property access on the entity parameter
        if (node.Expression is ParameterExpression)
            return sqlExpressionFactory.Property(node.Member.Name, node.Type);

        // Handle closure access (e.g., local variables captured in lambda)
        if (node.Expression is ConstantExpression constantExpression)
        {
            // Evaluate the member access to get the actual value
            var value = GetMemberValue(constantExpression.Value, node.Member);
            return sqlExpressionFactory.Constant(value, node.Type);
        }

        // Handle nested member access for closures
        if (node.Expression is MemberExpression nestedMember)
        {
            var parent = Visit(nestedMember);
            if (parent is SqlConstantExpression constantParent)
            {
                var value = GetMemberValue(constantParent.Value, node.Member);
                return sqlExpressionFactory.Constant(value, node.Type);
            }
        }

        return base.VisitMember(node);
    }

    private static object? GetMemberValue(object? instance, System.Reflection.MemberInfo member)
        => member switch
        {
            System.Reflection.FieldInfo field => field.GetValue(instance),
            System.Reflection.PropertyInfo property => property.GetValue(instance),
            _ => throw new NotSupportedException(
                $"Member type {member.MemberType} is not supported"),
        };

    /// <inheritdoc />
    protected override Expression VisitUnary(UnaryExpression node)
    {
        // Handle conversions and other unary operations
        if (node.NodeType == ExpressionType.Convert
            || node.NodeType == ExpressionType.ConvertChecked)
        {
            var operand = Visit(node.Operand);
            if (operand is SqlExpression sqlOperand)
                // Apply type mapping for the conversion
                return sqlExpressionFactory.ApplyTypeMapping(sqlOperand, node.Type);
        }

        return base.VisitUnary(node);
    }
}
