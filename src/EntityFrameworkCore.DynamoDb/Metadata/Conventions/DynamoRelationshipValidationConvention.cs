using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>Rejects EF Core foreign-key relationship configuration for DynamoDB models.</summary>
public sealed class DynamoRelationshipValidationConvention
    : IForeignKeyAddedConvention, IForeignKeyOwnershipChangedConvention, IModelFinalizingConvention
{
    /// <inheritdoc />
    public void ProcessForeignKeyAdded(
        IConventionForeignKeyBuilder foreignKeyBuilder,
        IConventionContext<IConventionForeignKeyBuilder> context)
        => ThrowIfUnsupportedRelationship(foreignKeyBuilder.Metadata);

    /// <inheritdoc />
    public void ProcessForeignKeyOwnershipChanged(
        IConventionForeignKeyBuilder relationshipBuilder,
        IConventionContext<bool?> context)
        => ThrowIfUnsupportedRelationship(relationshipBuilder.Metadata);

    /// <inheritdoc />
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            foreach (var foreignKey in entityType.GetDeclaredForeignKeys())
                ThrowIfUnsupportedRelationship(foreignKey);
        }
    }

    /// <summary>Throws when a non-ownership foreign key relationship is configured.</summary>
    private static void ThrowIfUnsupportedRelationship(IReadOnlyForeignKey foreignKey)
    {
        if (foreignKey.IsOwnership)
            return;

        throw DynamoModelValidationErrors.ForeignKeyRelationshipsNotSupported(foreignKey);
    }
}
