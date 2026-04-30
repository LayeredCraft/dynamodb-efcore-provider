using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;

#pragma warning disable EF1001 // Internal EF Core API: StructuralTypeMaterializerSource

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Overrides complex type materialization so that complex properties are handled by
///     <see cref="DynamoShapedQueryCompilingExpressionVisitor.AddStructuralTypeInitialization" />
///     rather than being inlined via <c>ValueBufferTryReadValue</c>.
///     This lets the projection-binding removing visitor inject the correct nested
///     <c>Dictionary&lt;string, AttributeValue&gt;</c> context per complex property.
/// </summary>
internal sealed class DynamoStructuralTypeMaterializerSource(
    StructuralTypeMaterializerSourceDependencies dependencies)
    : StructuralTypeMaterializerSource(dependencies)
{
    /// <summary>
    ///     Returns <see langword="false" /> so that complex type properties are skipped during
    ///     the initial inline scalar materialization pass and are instead emitted as
    ///     <see cref="DynamoComplexPropertyInitializationExpression" /> /
    ///     <see cref="DynamoComplexCollectionInitializationExpression" /> markers by
    ///     <c>AddStructuralTypeInitialization</c>.
    /// </summary>
    protected override bool ReadComplexTypeDirectly(IComplexType complexType) => false;
}
