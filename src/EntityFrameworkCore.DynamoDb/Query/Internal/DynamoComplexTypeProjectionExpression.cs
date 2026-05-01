using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Marker expression placed into the shaper expression tree when a complex property is
///     directly projected (e.g. <c>Select(e =&gt; e.Profile)</c> or an anonymous-type member
///     <c>Select(e =&gt; new { e.Pk, e.Profile })</c>).
/// </summary>
/// <remarks>
///     <para>
///         Unlike <see cref="DynamoComplexPropertyInitializationExpression" /> (which assigns
///         the materialized value into an owner's member), this expression represents the complex
///         value as the direct result of a projection.
///     </para>
///     <para>
///         <see cref="VisitChildren" /> delegates to the inner shaper so that
///         <c>InjectStructuralTypeMaterializers</c> can process the wrapped
///         <c>StructuralTypeShaperExpression</c>. After injection the inner shaper is replaced
///         with the materializer block. <see cref="DynamoProjectionBindingRemovingExpressionVisitor" />
///         then handles this node by pushing the correct nested
///         <c>Dictionary&lt;string, AttributeValue&gt;</c> context before visiting the injected
///         materializer, and returns the materialized value directly (nullable-guarded when the
///         property is nullable).
///     </para>
/// </remarks>
internal sealed class DynamoComplexTypeProjectionExpression(
    IComplexProperty complexProperty,
    Expression innerShaper) : Expression
{
    /// <summary>The complex property being projected.</summary>
    public IComplexProperty ComplexProperty { get; } = complexProperty;

    /// <summary>
    ///     The inner shaper — initially a <c>StructuralTypeShaperExpression</c>, replaced with the
    ///     injected materializer block after <c>InjectStructuralTypeMaterializers</c> runs.
    /// </summary>
    public Expression InnerShaper { get; } = innerShaper;

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    public override Type Type => InnerShaper.Type;

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var visited = visitor.Visit(InnerShaper);
        return ReferenceEquals(visited, InnerShaper)
            ? this
            : new DynamoComplexTypeProjectionExpression(ComplexProperty, visited);
    }

    /// <inheritdoc />
    public override string ToString()
        => $"ComplexTypeProjection({ComplexProperty.DeclaringType.DisplayName()}.{ComplexProperty.Name})";
}
