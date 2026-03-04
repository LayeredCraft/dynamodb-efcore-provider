using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
///     Represents a scalar property access on a nested document path, emitting dot-notation
///     PartiQL (e.g., <c>"Profile"."Address"."City"</c>). Supported in WHERE predicates.
/// </summary>
/// <remarks>
///     Forms a linked chain: the <see cref="Parent" /> expression represents the path up to this
///     point, and <see cref="PropertyName" /> appends the next segment. The root of any chain is a
///     <see cref="SqlPropertyExpression" /> for the top-level DynamoDB attribute.
/// </remarks>
public sealed class DynamoScalarAccessExpression(
    Expression parent,
    string propertyName,
    Type type,
    CoreTypeMapping? typeMapping = null) : SqlExpression(type, typeMapping)
{
    /// <summary>The parent expression representing the path up to this segment.</summary>
    public Expression Parent { get; } = parent;

    /// <summary>The DynamoDB attribute name for this path segment.</summary>
    public string PropertyName { get; } = propertyName;

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var visitedParent = visitor.Visit(Parent);
        return visitedParent == Parent
            ? this
            : new DynamoScalarAccessExpression(visitedParent, PropertyName, Type, TypeMapping);
    }

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        => new DynamoScalarAccessExpression(Parent, PropertyName, Type, typeMapping);

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Visit(Parent);
        expressionPrinter.Append("." + PropertyName);
    }

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is DynamoScalarAccessExpression o
            && base.Equals(o)
            && Parent.Equals(o.Parent)
            && PropertyName == o.PropertyName;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Parent, PropertyName);
}
