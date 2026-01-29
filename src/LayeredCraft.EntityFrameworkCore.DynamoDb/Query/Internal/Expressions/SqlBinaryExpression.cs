using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
/// Represents a binary operation in a SQL expression (e.g., =, <, AND, OR, +, -).
/// </summary>
public class SqlBinaryExpression(
    ExpressionType operatorType,
    SqlExpression left,
    SqlExpression right,
    Type type,
    CoreTypeMapping? typeMapping) : SqlExpression(type, typeMapping)
{
    /// <summary>
    /// The type of binary operation.
    /// </summary>
    public ExpressionType OperatorType { get; } = operatorType;

    /// <summary>
    /// The left operand.
    /// </summary>
    public SqlExpression Left { get; } = left;

    /// <summary>
    /// The right operand.
    /// </summary>
    public SqlExpression Right { get; } = right;

    /// <summary>
    /// Creates a new binary expression with updated operands.
    /// </summary>
    public SqlBinaryExpression Update(SqlExpression left, SqlExpression right)
        => left != Left || right != Right
            ? new SqlBinaryExpression(OperatorType, left, right, Type, TypeMapping)
            : this;

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        => new SqlBinaryExpression(OperatorType, Left, Right, Type, typeMapping);

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Append("(");
        expressionPrinter.Visit(Left);
        expressionPrinter.Append($" {GetOperatorString(OperatorType)} ");
        expressionPrinter.Visit(Right);
        expressionPrinter.Append(")");
    }

    private static string GetOperatorString(ExpressionType operatorType)
        => operatorType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.AndAlso => "AND",
            ExpressionType.OrElse => "OR",
            ExpressionType.Add => "+",
            ExpressionType.Subtract => "-",
            ExpressionType.Multiply => "*",
            ExpressionType.Divide => "/",
            _ => throw new NotSupportedException($"Operator type {operatorType} is not supported"),
        };

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is SqlBinaryExpression binaryExpression
           && base.Equals(binaryExpression)
           && OperatorType == binaryExpression.OperatorType
           && Left.Equals(binaryExpression.Left)
           && Right.Equals(binaryExpression.Right);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), OperatorType, Left, Right);
}
