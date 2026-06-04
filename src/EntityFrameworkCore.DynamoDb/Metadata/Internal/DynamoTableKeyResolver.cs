using EntityFrameworkCore.DynamoDb.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Metadata.Internal;

/// <summary>Resolves DynamoDB table-key roles from provider annotations and conventions.</summary>
internal static class DynamoTableKeyResolver
{
    public static DynamoResolvedTableKey Resolve(IConventionEntityType entityType)
    {
        var primaryKey = entityType.FindPrimaryKey();
        var partitionKeySource = entityType
            .FindAnnotation(DynamoAnnotationNames.PartitionKeyPropertyName)
            ?.GetConfigurationSource();
        var sortKeySource = entityType
            .FindAnnotation(DynamoAnnotationNames.SortKeyPropertyName)
            ?.GetConfigurationSource();

        var partitionKey = ResolveAnnotatedProperty(
                entityType,
                DynamoAnnotationNames.PartitionKeyPropertyName,
                partitionKeySource)
            ?? ResolvePartitionKeyByConvention(entityType);
        var sortKey = ResolveAnnotatedProperty(
                entityType,
                DynamoAnnotationNames.SortKeyPropertyName,
                sortKeySource)
            ?? ResolveSortKeyByConvention(entityType);

        return new DynamoResolvedTableKey(
            partitionKey,
            sortKey,
            primaryKey,
            primaryKey?.GetConfigurationSource(),
            partitionKeySource,
            sortKeySource);
    }

    private static IConventionProperty? ResolveAnnotatedProperty(
        IConventionEntityType entityType,
        string annotationName,
        ConfigurationSource? source)
    {
        if (source is not (ConfigurationSource.Explicit or ConfigurationSource.DataAnnotation))
            return null;

        var propertyName = entityType[annotationName] as string;
        return propertyName is null ? null : entityType.FindProperty(propertyName);
    }

    private static IConventionProperty? ResolvePartitionKeyByConvention(
        IConventionEntityType entityType)
    {
        var candidates = DynamoKeyDiscoveryConvention.GetPartitionKeyCandidates(
            entityType
                .GetProperties()
                .Where(DynamoKeyDiscoveryConvention.IsDiscoverableKeyProperty));

        return ResolveSingleConventionalCandidate(
            entityType,
            candidates,
            "partition key",
            "HasPartitionKey");
    }

    private static IConventionProperty? ResolveSortKeyByConvention(IConventionEntityType entityType)
    {
        var candidates = entityType
            .GetProperties()
            .Where(p => DynamoKeyDiscoveryConvention.IsDiscoverableKeyProperty(p)
                && DynamoKeyDiscoveryConvention.IsSortKeyName(p.Name))
            .ToList();

        return ResolveSingleConventionalCandidate(entityType, candidates, "sort key", "HasSortKey");
    }

    private static IConventionProperty? ResolveSingleConventionalCandidate(
        IConventionEntityType entityType,
        List<IConventionProperty> candidates,
        string roleName,
        string fluentApiName)
    {
        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        var names = string.Join(", ", candidates.Select(p => $"'{p.Name}'"));
        throw new InvalidOperationException(
            $"Entity type '{entityType.DisplayName()}' has multiple properties with conventional "
            + $"{roleName} names ({names}). The convention cannot determine which is the DynamoDB "
            + $"{roleName}. Call {fluentApiName}() to specify the property explicitly.");
    }
}
