using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>Represents an explicitly parenthesized SQL expression.</summary>
public sealed class SqlParenthesizedExpression(
    SqlExpression operand,
    CoreTypeMapping? typeMapping = null) : SqlExpression(
    operand.Type,
    typeMapping ?? operand.TypeMapping)
{
    /// <summary>The expression wrapped in parentheses.</summary>
    public SqlExpression Operand { get; } = operand;

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Append("(");
        expressionPrinter.Visit(Operand);
        expressionPrinter.Append(")");
    }

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        => new SqlParenthesizedExpression(Operand, typeMapping);

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is SqlParenthesizedExpression parenthesizedExpression
            && base.Equals(parenthesizedExpression)
            && Operand.Equals(parenthesizedExpression.Operand);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Operand);
}
