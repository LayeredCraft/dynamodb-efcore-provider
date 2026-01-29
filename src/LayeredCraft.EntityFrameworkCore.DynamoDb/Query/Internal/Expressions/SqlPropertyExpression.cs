using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
/// Represents a property access in a SQL expression (e.g., Item.Name).
/// </summary>
public class SqlPropertyExpression(string propertyName, Type type, CoreTypeMapping? typeMapping)
    : SqlExpression(type, typeMapping)
{
    /// <summary>
    /// The name of the property being accessed.
    /// </summary>
    public string PropertyName { get; } = propertyName;

    /// <summary>
    /// Creates a new property expression with the specified type mapping.
    /// </summary>
    public SqlPropertyExpression ApplyTypeMapping(CoreTypeMapping? typeMapping)
        => new(PropertyName, Type, typeMapping);

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        => new SqlPropertyExpression(PropertyName, Type, typeMapping);

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
        => expressionPrinter.Append(PropertyName);

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is SqlPropertyExpression propertyExpression
           && base.Equals(propertyExpression)
           && PropertyName == propertyExpression.PropertyName;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), PropertyName);
}
