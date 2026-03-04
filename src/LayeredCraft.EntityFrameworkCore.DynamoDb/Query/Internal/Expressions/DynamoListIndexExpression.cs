using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
///     Represents a list element access by integer index, emitting bracket-notation PartiQL
///     (e.g., <c>"Tags"[0]</c> or <c>"Profile"."Tags"[0]</c>). Composes with
///     <see cref="DynamoScalarAccessExpression" /> for arbitrarily nested paths.
/// </summary>
/// <remarks>
///     The index is a literal <see cref="int" /> rather than an expression because DynamoDB
///     PartiQL path notation requires integer literals — list indexes are never parameterized.
/// </remarks>
public sealed class DynamoListIndexExpression(
    Expression source,
    int index,
    Type type,
    CoreTypeMapping? typeMapping = null) : SqlExpression(type, typeMapping)
{
    /// <summary>The collection expression whose element is being accessed.</summary>
    public Expression Source { get; } = source;

    /// <summary>The zero-based integer index into the list.</summary>
    public int Index { get; } = index;

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var visitedSource = visitor.Visit(Source);
        return visitedSource == Source
            ? this
            : new DynamoListIndexExpression(visitedSource, Index, Type, TypeMapping);
    }

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        => new DynamoListIndexExpression(Source, Index, Type, typeMapping);

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Visit(Source);
        expressionPrinter.Append($"[{Index}]");
    }

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is DynamoListIndexExpression o
            && base.Equals(o)
            && Source.Equals(o.Source)
            && Index == o.Index;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Source, Index);
}
