using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
/// Represents a property access in a SQL expression (e.g., Item.Name).
/// </summary>
public class SqlPropertyExpression(
    string propertyName,
    Type type,
    CoreTypeMapping? typeMapping,
    bool isPartitionKey = false)
    : SqlExpression(type, typeMapping)
{
    /// <summary>
    /// The name of the property being accessed.
    /// </summary>
    public string PropertyName { get; } = propertyName;

    /// <summary>Indicates whether this property maps to the configured partition key.</summary>
    public bool IsPartitionKey { get; } = isPartitionKey;

    /// <summary>
    /// Creates a new property expression with the specified type mapping.
    /// </summary>
    public SqlPropertyExpression ApplyTypeMapping(CoreTypeMapping? typeMapping)
        => new(PropertyName, Type, typeMapping, IsPartitionKey);

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        => new SqlPropertyExpression(PropertyName, Type, typeMapping, IsPartitionKey);

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
        => expressionPrinter.Append(PropertyName);

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is SqlPropertyExpression propertyExpression
           && base.Equals(propertyExpression)
           && PropertyName == propertyExpression.PropertyName
           && IsPartitionKey == propertyExpression.IsPartitionKey;

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), PropertyName, IsPartitionKey);
}
