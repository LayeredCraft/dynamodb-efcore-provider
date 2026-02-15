using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>
///     Configures complex navigation target types as owned entities during relationship
///     discovery.
/// </summary>
public sealed class DynamoRelationshipDiscoveryConvention(
    ProviderConventionSetBuilderDependencies dependencies)
    : RelationshipDiscoveryConvention(dependencies)
{
    /// <summary>Decides whether a navigation target type should be discovered as owned by convention.</summary>
    protected override bool? ShouldBeOwned(Type targetType, IConventionModel model)
        => ShouldBeOwnedType(targetType);

    /// <summary>Determines whether the target CLR type should be treated as an owned type.</summary>
    public static bool ShouldBeOwnedType(Type targetType)
    {
        var nonNullableType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        return !DynamoTypeMappingSource.IsPrimitiveType(nonNullableType)
            && !DynamoTypeMappingSource.IsSupportedPrimitiveCollectionShape(nonNullableType);
    }
}
