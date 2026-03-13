using LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>Configures default discriminator metadata for shared DynamoDB table mappings.</summary>
/// <remarks>
///     A discriminator is conventionally configured when multiple concrete entity types are
///     mapped to the same table group. The discriminator property name comes from
///     <see cref="IReadOnlyModel.GetEmbeddedDiscriminatorName" />, which defaults to <c>$type</c>.
/// </remarks>
public sealed class DynamoDiscriminatorConvention : IModelFinalizingConvention
{
    /// <summary>Applies discriminator conventions to table groups that map multiple concrete entity types.</summary>
    /// <param name="modelBuilder">The convention model builder.</param>
    /// <param name="context">The convention context.</param>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        var rootEntityTypes = modelBuilder.Metadata
            .GetEntityTypes()
            .OfType<IConventionEntityType>()
            .Where(static entityType => !entityType.IsOwned()
                && entityType.FindOwnership() is null
                && entityType.BaseType is null)
            .ToList();

        foreach (var tableGroup in rootEntityTypes.GroupBy(static entityType => entityType.GetTableGroupName()))
        {
            var concreteEntityTypes = tableGroup
                .SelectMany(static entityType => entityType.GetConcreteDerivedTypesInclusive())
                .Where(static entityType => !entityType.IsOwned())
                .Distinct()
                .ToList();

            if (concreteEntityTypes.Count <= 1)
                continue;

            foreach (var rootEntityType in tableGroup)
            {
                var discriminatorBuilder = rootEntityType.Builder.HasDiscriminator(
                    rootEntityType.Model.GetEmbeddedDiscriminatorName(),
                    typeof(string));

                if (discriminatorBuilder is null)
                    continue;

                SetDefaultDiscriminatorValues(
                    rootEntityType.GetDerivedTypesInclusive(),
                    discriminatorBuilder);
            }
        }
    }

    /// <summary>Sets default discriminator values to each entity type's short name.</summary>
    private static void SetDefaultDiscriminatorValues(
        IEnumerable<IConventionEntityType> entityTypes,
        IConventionDiscriminatorBuilder discriminatorBuilder)
    {
        foreach (var entityType in entityTypes)
            discriminatorBuilder.HasValue(entityType, entityType.ShortName());
    }
    /// <summary>Gets the table-group key used for shared-table discriminator convention decisions.</summary>
    private static string GetTableGroupName(IConventionEntityType entityType)
        => entityType.GetTableGroupName();
}
