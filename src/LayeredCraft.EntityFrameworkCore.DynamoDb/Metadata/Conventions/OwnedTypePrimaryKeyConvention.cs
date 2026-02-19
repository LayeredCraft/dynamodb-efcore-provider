using LayeredCraft.EntityFrameworkCore.DynamoDb.ValueGeneration.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>Ensures owned collection element types have an ordinal key for change tracking.</summary>
public sealed class OwnedTypePrimaryKeyConvention : IModelFinalizingConvention
{
    private const string OwnedOrdinalPropertyBaseName = "__OwnedOrdinal";

    /// <summary>Adds or configures ordinal key properties for owned collection elements.</summary>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            var ownership = entityType.FindOwnership();
            if (ownership is not { IsUnique: false } || ownership.DeclaringEntityType != entityType)
                continue;

            var ordinalProperty =
                entityType.GetProperties().FirstOrDefault(p => p.IsOwnedOrdinalKeyProperty());

            if (ordinalProperty is null)
            {
                ordinalProperty = CreateOrdinalProperty(entityType);

                if (ordinalProperty is null)
                    continue;

                var keyProperties = ownership.Properties.ToList();
                keyProperties.Add(ordinalProperty);

                entityType.Builder.PrimaryKey(keyProperties);
            }

            ordinalProperty.Builder.ValueGenerated(ValueGenerated.OnAdd);
            ordinalProperty.Builder.HasValueGenerator((_, _) => new OwnedOrdinalValueGenerator());
        }
    }

    private static IConventionProperty? CreateOrdinalProperty(IConventionEntityType entityType)
    {
        var propertyBuilder = entityType.Builder.CreateUniqueProperty(
            typeof(int),
            OwnedOrdinalPropertyBaseName,
            true);

        return propertyBuilder?.Metadata;
    }
}
