using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>
///     Adds a provider-managed <c>$version</c> shadow property to every root (non-owned) entity
///     type. The version is set to <c>1</c> on INSERT and incremented on every UPDATE, forming the
///     predicate for optimistic concurrency checks without requiring any user model changes.
/// </summary>
/// <remarks>
///     Mirrors the <see cref="DynamoDiscriminatorConvention" /> pattern. Run after the
///     discriminator convention so that the model is fully formed when this convention executes.
/// </remarks>
public sealed class DynamoVersionConvention : IModelFinalizingConvention
{
    /// <summary>The DynamoDB attribute name written for the provider-managed version stamp.</summary>
    public const string PropertyName = "$version";

    /// <summary>Adds the <c>$version</c> shadow property to all root non-owned entity types.</summary>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder
                .Metadata
                .GetEntityTypes()
                // Only root, non-owned entities — owned entities share their root's item.
                .Where(static e => !e.IsOwned() && e.FindOwnership() is null && e.BaseType is null))
            // Use long? so that items written before $version support was introduced can be
            // materialized without error (missing attribute → null → treated as 0 in write logic).
            // Items first written with EF Core after this convention is active will always carry
            // $version = 1, so the zero default is only a transitional state for legacy items.
            //
            // No ValueGenerated or value generator is configured here — the initial value of 1
            // is set explicitly in DynamoDatabaseWrapper for Added entries, and subsequent
            // increments are applied there too. Using EF's value-generator system would interfere
            // with original-value tracking for queried entities.
            entityType.Builder.Property(typeof(long?), PropertyName);
    }
}
