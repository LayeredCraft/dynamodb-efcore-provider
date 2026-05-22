using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>Rejects owned entity type configuration with a provider-specific guidance message.</summary>
/// <remarks>Only <see cref="IModelFinalizingConvention"/> is implemented deliberately — early-fire hooks were
/// removed because validation deferred to finalization is sufficient and avoids double-registration.</remarks>
public sealed class DynamoOwnedEntityTypeValidationConvention : IModelFinalizingConvention
{
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
