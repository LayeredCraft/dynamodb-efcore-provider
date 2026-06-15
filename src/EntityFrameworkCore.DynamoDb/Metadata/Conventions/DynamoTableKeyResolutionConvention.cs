using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>Finalizes DynamoDB table-key annotations and EF primary-key shape.</summary>
public sealed class DynamoTableKeyResolutionConvention : IModelFinalizingConvention
{
    /// <inheritdoc />
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (entityType.BaseType != null || entityType.IsOwned())
                continue;

            var resolvedKey = DynamoTableKeyResolver.Resolve(entityType);
            ApplyResolvedKey(entityType, resolvedKey);
        }
    }

    private static void ApplyResolvedKey(
        IConventionEntityType entityType,
        DynamoResolvedTableKey resolvedKey)
    {
        if (resolvedKey.PartitionKey is null)
            return;

        entityType.SetOrRemoveAnnotation(
            DynamoAnnotationNames.PartitionKeyPropertyName,
            resolvedKey.PartitionKey.Name);
        entityType.SetOrRemoveAnnotation(
            DynamoAnnotationNames.SortKeyPropertyName,
            resolvedKey.SortKey?.Name);

        if (entityType.Builder is null)
            return;

        if (resolvedKey.PrimaryKey != null
            && !ConfigurationSource.Convention.Overrides(resolvedKey.PrimaryKeyConfigurationSource))
            return;

        var keyProperties = new List<IConventionProperty> { resolvedKey.PartitionKey };
        if (resolvedKey.SortKey is not null && !keyProperties.Contains(resolvedKey.SortKey))
            keyProperties.Add(resolvedKey.SortKey);

        if (resolvedKey.PrimaryKey != null
            && resolvedKey.PrimaryKey.Properties.SequenceEqual(keyProperties))
            return;

        if (resolvedKey.PrimaryKey != null)
            resolvedKey.PrimaryKey.DeclaringEntityType.Builder.HasNoKey(resolvedKey.PrimaryKey);

        entityType.Builder.PrimaryKey(keyProperties);
    }
}
