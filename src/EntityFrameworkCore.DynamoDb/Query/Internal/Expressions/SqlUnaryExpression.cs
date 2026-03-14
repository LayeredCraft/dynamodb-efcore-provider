using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>Represents a unary operation in a SQL expression (e.g., NOT).</summary>
public sealed class SqlUnaryExpression : SqlExpression
{
    /// <summary>Initializes a new unary expression.</summary>
    ///     The unary operator type; only <c>ExpressionType.Not</c> is
    ///     supported.
    /// <exception cref="NotSupportedException">
    ///     Thrown when <paramref name="operatorType" /> is not
    ///     <c>ExpressionType.Not</c>.
    /// </exception>
    public SqlUnaryExpression(
        ExpressionType operatorType,
        SqlExpression operand,
        CoreTypeMapping? typeMapping = null) : base(typeof(bool), typeMapping)
    {
        if (operatorType != ExpressionType.Not)
            throw new NotSupportedException(
                $"Unary operator '{operatorType}' is not supported. Only ExpressionType.Not is valid.");

        OperatorType = operatorType;
        Operand = operand;
    }

    /// <summary>The unary operator type.</summary>
    public ExpressionType OperatorType { get; }

    /// <summary>The operand expression.</summary>
    public SqlExpression Operand { get; }

    /// <summary>Creates a new expression with an updated operand.</summary>
    public SqlUnaryExpression Update(SqlExpression operand)
        => operand != Operand ? new SqlUnaryExpression(OperatorType, operand, TypeMapping) : this;

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        => new SqlUnaryExpression(OperatorType, Operand, typeMapping);

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Append("NOT(");
        expressionPrinter.Visit(Operand);
        expressionPrinter.Append(")");
    }

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is SqlUnaryExpression unary
            && base.Equals(unary)
            && OperatorType == unary.OperatorType
            && Operand.Equals(unary.Operand);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), OperatorType, Operand);
}
