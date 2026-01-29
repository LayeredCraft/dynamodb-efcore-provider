using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
/// Represents a constant value in a SQL expression.
/// </summary>
public class SqlConstantExpression(object? value, Type type, CoreTypeMapping? typeMapping)
    : SqlExpression(type, typeMapping)
{
    /// <summary>
    /// The constant value.
    /// </summary>
    public object? Value { get; } = value;

    /// <summary>
    /// Creates a new constant expression with the specified type mapping.
    /// </summary>
    public SqlConstantExpression ApplyTypeMapping(CoreTypeMapping? typeMapping)
        => new(Value, Type, typeMapping);

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        => new SqlConstantExpression(Value, Type, typeMapping);

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
        => expressionPrinter.Append(Value?.ToString() ?? "NULL");

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is SqlConstantExpression constantExpression
           && base.Equals(constantExpression)
           && Equals(Value, constantExpression.Value);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Value);
}
