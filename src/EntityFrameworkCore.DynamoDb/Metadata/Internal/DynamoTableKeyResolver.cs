using EntityFrameworkCore.DynamoDb.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Metadata.Internal;

/// <summary>Resolves DynamoDB table-key roles from provider annotations and conventions.</summary>
internal static class DynamoTableKeyResolver
{
    public static DynamoResolvedTableKey Resolve(IConventionEntityType entityType)
    {
        var primaryKey = entityType.FindPrimaryKey();
        var primaryKeySource = primaryKey?.GetConfigurationSource();
        var partitionKeySource = entityType
            .FindAnnotation(DynamoAnnotationNames.PartitionKeyPropertyName)
            ?.GetConfigurationSource();
        var sortKeySource = entityType
            .FindAnnotation(DynamoAnnotationNames.SortKeyPropertyName)
            ?.GetConfigurationSource();

        var annotatedPartitionKey = ResolveAnnotatedProperty(
            entityType,
            DynamoAnnotationNames.PartitionKeyPropertyName,
            partitionKeySource,
            "partition key");
        var annotatedSortKey = ResolveAnnotatedProperty(
            entityType,
            DynamoAnnotationNames.SortKeyPropertyName,
            sortKeySource,
            "sort key");

        var primaryKeyIsExplicit = primaryKeySource is ConfigurationSource.Explicit
            or ConfigurationSource.DataAnnotation;
        if (primaryKeyIsExplicit && primaryKey is not null)
            return ResolveExplicitPrimaryKey(
                entityType,
                primaryKey,
                primaryKeySource,
                annotatedPartitionKey,
                annotatedSortKey,
                partitionKeySource,
                sortKeySource);

        var partitionKey = annotatedPartitionKey ?? ResolvePartitionKeyByConvention(entityType);
        var sortKey = annotatedSortKey ?? ResolveSortKeyByConvention(entityType);

        if (partitionKey is null
            && primaryKeySource == ConfigurationSource.Convention
            && primaryKey is { Properties.Count: <= 2 })
        {
            partitionKey = primaryKey.Properties[0];
            sortKey ??= primaryKey.Properties.Count == 2 ? primaryKey.Properties[1] : null;
        }

        ValidateResolvedRoles(entityType, partitionKey, sortKey);

        return new DynamoResolvedTableKey(
            partitionKey,
            sortKey,
            primaryKey,
            primaryKeySource,
            partitionKeySource,
            sortKeySource);
    }

    private static DynamoResolvedTableKey ResolveExplicitPrimaryKey(
        IConventionEntityType entityType,
        IConventionKey primaryKey,
        ConfigurationSource? primaryKeySource,
        IConventionProperty? annotatedPartitionKey,
        IConventionProperty? annotatedSortKey,
        ConfigurationSource? partitionKeySource,
        ConfigurationSource? sortKeySource)
    {
        if (primaryKey.Properties.Count > 2)
            throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' has EF primary key "
                + $"[{FormatProperties(primaryKey.Properties)}], but DynamoDB table keys support only "
                + "one- or two-part keys: partition key with optional sort key.");

        var primaryPartitionKey = primaryKey.Properties[0];
        var primarySortKey = primaryKey.Properties.Count == 2 ? primaryKey.Properties[1] : null;

        if (annotatedPartitionKey is not null && annotatedPartitionKey == annotatedSortKey)
            ValidateResolvedRoles(entityType, annotatedPartitionKey, annotatedSortKey);

        if (IsExplicitProviderKey(partitionKeySource)
            && annotatedPartitionKey is not null
            && annotatedPartitionKey != primaryPartitionKey)
            throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' configures DynamoDB partition key "
                + $"'{annotatedPartitionKey.Name}', but its EF primary key starts with "
                + $"'{primaryPartitionKey.Name}'. Configure HasKey(...) and HasPartitionKey(...) to "
                + "use the same first property.");

        if (IsExplicitProviderKey(sortKeySource) && annotatedSortKey is not null)
        {
            if (annotatedSortKey == primaryPartitionKey)
                ValidateResolvedRoles(entityType, primaryPartitionKey, annotatedSortKey);

            if (primarySortKey is null)
                throw new InvalidOperationException(
                    $"Entity type '{entityType.DisplayName()}' configures DynamoDB sort key "
                    + $"'{annotatedSortKey.Name}', but its EF primary key has only one property. "
                    + "Use a two-part HasKey(...) or remove HasSortKey(...).");

            if (annotatedSortKey != primarySortKey)
                throw new InvalidOperationException(
                    $"Entity type '{entityType.DisplayName()}' configures DynamoDB sort key "
                    + $"'{annotatedSortKey.Name}', but the second EF primary-key property is "
                    + $"'{primarySortKey.Name}'. Configure HasKey(...) and HasSortKey(...) to use "
                    + "the same second property.");
        }

        var partitionKey = annotatedPartitionKey ?? primaryPartitionKey;
        var sortKey = annotatedSortKey ?? primarySortKey;
        ValidateResolvedRoles(entityType, partitionKey, sortKey);

        return new DynamoResolvedTableKey(
            partitionKey,
            sortKey,
            primaryKey,
            primaryKeySource,
            partitionKeySource,
            sortKeySource);
    }

    private static IConventionProperty? ResolveAnnotatedProperty(
        IConventionEntityType entityType,
        string annotationName,
        ConfigurationSource? source,
        string roleName)
    {
        if (!IsExplicitProviderKey(source))
            return null;

        var propertyName = entityType[annotationName] as string;
        if (propertyName is null)
            return null;

        return entityType.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"The {roleName} property '{propertyName}' configured on entity type "
                + $"'{entityType.DisplayName()}' does not exist.");
    }

    private static bool IsExplicitProviderKey(ConfigurationSource? source)
        => source is ConfigurationSource.Explicit or ConfigurationSource.DataAnnotation;

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

    private static void ValidateResolvedRoles(
        IConventionEntityType entityType,
        IConventionProperty? partitionKey,
        IConventionProperty? sortKey)
    {
        if (partitionKey is not null && partitionKey == sortKey)
            throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' maps property '{partitionKey.Name}' as both "
                + "the DynamoDB partition key and sort key. Use different properties for each key role.");

        if (partitionKey is null && sortKey is not null)
            throw new InvalidOperationException(
                $"No DynamoDB partition key is configured for entity type '{entityType.DisplayName()}'. "
                + $"Sort key property '{sortKey.Name}' is configured but no partition key can be determined. "
                + "Call HasPartitionKey(...) or use a one- or two-part HasKey(...).");
    }

    private static string FormatProperties(IEnumerable<IReadOnlyProperty> properties)
        => string.Join(", ", properties.Select(static p => p.Name));
}
