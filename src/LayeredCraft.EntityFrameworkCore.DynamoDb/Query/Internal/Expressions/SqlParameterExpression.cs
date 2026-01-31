using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
/// Represents a parameter in a SQL expression that will be substituted at execution time.
/// </summary>
public class SqlParameterExpression(string name, Type type, CoreTypeMapping? typeMapping)
    : SqlExpression(type, typeMapping)
{
    /// <summary>
    /// The parameter name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Creates a new parameter expression with the specified type mapping.
    /// </summary>
    public SqlParameterExpression ApplyTypeMapping(CoreTypeMapping? typeMapping)
        => new(Name, Type, typeMapping);

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        => new SqlParameterExpression(Name, Type, typeMapping);

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
        => expressionPrinter.Append($"@{Name}");

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is SqlParameterExpression parameterExpression
           && base.Equals(parameterExpression)
           && Name == parameterExpression.Name;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Name);
}
