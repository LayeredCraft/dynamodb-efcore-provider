using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>Rejects owned entity type configuration with a provider-specific guidance message.</summary>
public sealed class DynamoOwnedEntityTypeValidationConvention
    : IEntityTypeAddedConvention, IForeignKeyOwnershipChangedConvention, IModelFinalizingConvention
{
    /// <inheritdoc />
    public void ProcessEntityTypeAdded(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionContext<IConventionEntityTypeBuilder> context)
        => ThrowIfOwned(entityTypeBuilder.Metadata);

    /// <inheritdoc />
    public void ProcessForeignKeyOwnershipChanged(
        IConventionForeignKeyBuilder relationshipBuilder,
        IConventionContext<bool?> context)
    {
        if (relationshipBuilder.Metadata.IsOwnership)
            ThrowIfOwned(relationshipBuilder.Metadata.DeclaringEntityType);
    }

    /// <inheritdoc />
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
            ThrowIfOwned(entityType);
    }

    /// <summary>Throws when an entity type is configured as owned.</summary>
    private static void ThrowIfOwned(IConventionEntityType entityType)
    {
        if (!entityType.IsOwned())
            return;

        throw DynamoModelValidationErrors.OwnedEntityTypesNotSupported(entityType);
    }
}
