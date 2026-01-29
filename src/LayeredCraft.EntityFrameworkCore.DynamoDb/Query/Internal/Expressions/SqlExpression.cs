using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
/// Base class for all SQL expressions in the DynamoDB query model.
/// </summary>
public abstract class SqlExpression(Type type, CoreTypeMapping? typeMapping) : Expression
{
    /// <summary>
    /// The CLR type of the expression result.
    /// </summary>
    public override Type Type { get; } = type;

    /// <summary>
    /// The type mapping for converting between CLR types and DynamoDB AttributeValues.
    /// </summary>
    public virtual CoreTypeMapping? TypeMapping { get; } = typeMapping;

    /// <summary>
    /// All SQL expressions are extension nodes.
    /// </summary>
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <summary>
    /// Returns a string representation for debugging purposes.
    /// </summary>
    public abstract void Print(ExpressionPrinter expressionPrinter);

    /// <summary>
    /// Creates a new expression with the specified type mapping applied.
    /// </summary>
    protected abstract SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SqlExpression other && Equals(other);

    protected virtual bool Equals(SqlExpression? other)
        => other != null && Type == other.Type && TypeMapping?.Equals(other.TypeMapping) == true;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Type, TypeMapping);
}
