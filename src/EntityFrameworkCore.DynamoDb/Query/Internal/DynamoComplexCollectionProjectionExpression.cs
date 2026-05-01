using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Marker expression placed into the shaper expression tree when a complex collection is
///     directly projected, for example <c>Select(e =&gt; e.Orders)</c> or
///     <c>Select(e =&gt; new { e.Pk, e.Orders })</c>.
/// </summary>
/// <remarks>
///     <see cref="VisitChildren" /> delegates to the element shaper so that structural
///     materializer injection can replace it with the per-element materializer block before
///     <see cref="DynamoProjectionBindingRemovingExpressionVisitor" /> reads the underlying
///     <c>AttributeValue.L</c> payload and materializes the collection.
/// </remarks>
internal sealed class DynamoComplexCollectionProjectionExpression(
    IComplexProperty complexProperty,
    Expression elementInnerShaper) : Expression
{
    /// <summary>The complex collection property being projected.</summary>
    public IComplexProperty ComplexProperty { get; } = complexProperty;

    /// <summary>
    ///     The per-element shaper, replaced with an injected materializer during structural-type
    ///     materializer injection.
    /// </summary>
    public Expression ElementInnerShaper { get; } = elementInnerShaper;

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    public override Type Type => ComplexProperty.ClrType;

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var visited = visitor.Visit(ElementInnerShaper);
        return ReferenceEquals(visited, ElementInnerShaper)
            ? this
            : new DynamoComplexCollectionProjectionExpression(ComplexProperty, visited);
    }

    /// <inheritdoc />
    public override string ToString()
        => $"ComplexCollectionProjection({ComplexProperty.DeclaringType.DisplayName()}.{ComplexProperty.Name})";
}
