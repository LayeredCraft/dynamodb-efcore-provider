using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Marker expression placed into the shaper expression tree by
///     <see cref="DynamoShapedQueryCompilingExpressionVisitor.AddStructuralTypeInitialization" />
///     for each non-collection complex property.
/// </summary>
/// <remarks>
///     The projection-binding removing visitor detects this node, reads the complex property's
///     <c>AttributeValue.M</c> map from the current attribute context, pushes the nested map
///     onto <c>_attributeContextStack</c>, visits the injected scalar materializer, then pops
///     the stack — wiring all <c>ValueBufferTryReadValue</c> calls inside the materializer to
///     the correct nested dictionary.
/// </remarks>
internal sealed class DynamoComplexPropertyInitializationExpression(
    IComplexProperty complexProperty,
    Expression injectedMaterializer,
    MemberExpression memberAccess) : Expression
{
    /// <summary>The complex property being initialized.</summary>
    public IComplexProperty ComplexProperty { get; } = complexProperty;

    /// <summary>
    ///     The injected materializer for the complex type's scalar properties, produced by
    ///     <c>InjectStructuralTypeMaterializers</c> on a synthetic shaper for the complex type.
    /// </summary>
    public Expression InjectedMaterializer { get; } = injectedMaterializer;

    /// <summary>
    ///     Member access expression targeting the property on the owner instance,
    ///     e.g. <c>ownerInstance.Address</c>. The removing visitor assigns the materialized
    ///     complex value to this member.
    /// </summary>
    public MemberExpression MemberAccess { get; } = memberAccess;

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    public override Type Type => typeof(void);

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var newMaterializer = visitor.Visit(InjectedMaterializer);
        var newMemberAccess = (MemberExpression)visitor.Visit(MemberAccess);
        return newMaterializer == InjectedMaterializer && newMemberAccess == MemberAccess
            ? this
            : new DynamoComplexPropertyInitializationExpression(
                ComplexProperty,
                newMaterializer,
                newMemberAccess);
    }
}
