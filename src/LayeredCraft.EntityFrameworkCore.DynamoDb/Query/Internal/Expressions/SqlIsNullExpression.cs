using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>Specifies the IS NULL/MISSING predicate variant.</summary>
public enum IsNullOperator
{
    /// <summary>Tests that an attribute has the DynamoDB NULL type.</summary>
    IsNull,

    /// <summary>Tests that an attribute does not have the DynamoDB NULL type.</summary>
    IsNotNull,

    /// <summary>Tests that an attribute is absent from the item.</summary>
    IsMissing,

    /// <summary>Tests that an attribute is present in the item.</summary>
    IsNotMissing,
}

/// <summary>Represents a PartiQL IS NULL, IS NOT NULL, IS MISSING, or IS NOT MISSING predicate.</summary>
public sealed class SqlIsNullExpression : SqlExpression
{
    /// <summary>Initializes a new IS NULL/MISSING expression.</summary>
    /// <param name="operand">The attribute expression being tested.</param>
    /// <param name="operator">The specific IS predicate variant to apply.</param>
    public SqlIsNullExpression(SqlExpression operand, IsNullOperator @operator) : base(
        typeof(bool),
        null)
    {
        Operand = operand;
        Operator = @operator;
    }

    private SqlIsNullExpression(
        SqlExpression operand,
        IsNullOperator @operator,
        CoreTypeMapping? typeMapping) : base(typeof(bool), typeMapping)
    {
        Operand = operand;
        Operator = @operator;
    }

    /// <summary>The attribute expression being tested.</summary>
    public SqlExpression Operand { get; }

    /// <summary>The IS predicate variant.</summary>
    public IsNullOperator Operator { get; }

    /// <summary>
    ///     Creates a new expression with an updated operand, returning <see langword="this" /> if
    ///     unchanged.
    /// </summary>
    public SqlIsNullExpression Update(SqlExpression operand)
        => !operand.Equals(Operand)
            ? new SqlIsNullExpression(operand, Operator, TypeMapping)
            : this;

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        => new SqlIsNullExpression(Operand, Operator, typeMapping);

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Visit(Operand);
        expressionPrinter.Append(
            Operator switch
            {
                IsNullOperator.IsNull => " IS NULL",
                IsNullOperator.IsNotNull => " IS NOT NULL",
                IsNullOperator.IsMissing => " IS MISSING",
                IsNullOperator.IsNotMissing => " IS NOT MISSING",
                _ => throw new NotSupportedException($"IS operator '{Operator}' is not supported."),
            });
    }

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is SqlIsNullExpression isNull
            && base.Equals(isNull)
            && Operator == isNull.Operator
            && Operand.Equals(isNull.Operand);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Operator, Operand);
}
