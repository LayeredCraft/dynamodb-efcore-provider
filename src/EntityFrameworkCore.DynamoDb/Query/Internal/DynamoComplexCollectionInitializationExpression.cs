using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Marker expression placed into the shaper expression tree by
///     <see cref="DynamoShapedQueryCompilingExpressionVisitor.AddStructuralTypeInitialization" />
///     for each collection complex property.
/// </summary>
/// <remarks>
///     The projection-binding removing visitor detects this node, reads the complex collection's
///     <c>AttributeValue.L</c> list, iterates over element maps, and for each element pushes the
///     element map onto <c>_attributeContextStack</c>, visits the per-element injected materializer,
///     then pops the stack — collecting materialized elements into the target list type.
/// </remarks>
internal sealed class DynamoComplexCollectionInitializationExpression(
    IComplexProperty complexProperty,
    Expression elementInjectedMaterializer,
    MemberExpression memberAccess) : Expression
{
    /// <summary>The complex collection property being initialized.</summary>
    public IComplexProperty ComplexProperty { get; } = complexProperty;

    /// <summary>
    ///     The per-element injected materializer for the complex element type's scalar properties,
    ///     produced by <c>InjectStructuralTypeMaterializers</c> on a synthetic shaper for the
    ///     element type.
    /// </summary>
    public Expression ElementInjectedMaterializer { get; } = elementInjectedMaterializer;

    /// <summary>
    ///     Member access expression targeting the collection property on the owner instance,
    ///     e.g. <c>ownerInstance.Lines</c>. The removing visitor assigns the materialized list
    ///     to this member.
    /// </summary>
    public MemberExpression MemberAccess { get; } = memberAccess;

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    public override Type Type => typeof(void);

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var newMaterializer = visitor.Visit(ElementInjectedMaterializer);
        var newMemberAccess = visitor.Visit(MemberAccess) as MemberExpression
            ?? throw new InvalidOperationException(
                $"Expected {nameof(MemberExpression)} after visiting {nameof(MemberAccess)} in {nameof(DynamoComplexCollectionInitializationExpression)}.");
        return newMaterializer == ElementInjectedMaterializer && newMemberAccess == MemberAccess
            ? this
            : new DynamoComplexCollectionInitializationExpression(
                ComplexProperty,
                newMaterializer,
                newMemberAccess);
    }
}
