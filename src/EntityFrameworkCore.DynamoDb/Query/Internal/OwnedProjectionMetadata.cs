using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Provides shared owned-projection metadata helpers for DynamoDB query shaping.</summary>
internal static class OwnedProjectionMetadata
{
    /// <summary>Determines whether an entity type is a non-owned root entity with a primary key.</summary>
    internal static bool IsRootEntity(IEntityType entityType)
        => !entityType.IsOwned() && entityType.FindPrimaryKey() != null;

    /// <summary>Gets immediate owned container attribute names for a root entity type.</summary>
    internal static HashSet<string> GetTopLevelOwnedContainingAttributeNames(IEntityType entityType)
    {
        HashSet<string> topLevelNames = new(StringComparer.Ordinal);
        if (!IsRootEntity(entityType))
            return topLevelNames;

        foreach (var projectionEntityType in GetProjectionEntityTypes(entityType))
            foreach (var navigation in projectionEntityType.GetDeclaredNavigations())
            {
                if (!navigation.IsEmbedded() || !navigation.TargetEntityType.IsOwned())
                    continue;

                var containingAttributeName =
                    navigation.TargetEntityType.GetContainingAttributeName() ?? navigation.Name;
                topLevelNames.Add(containingAttributeName);
            }

        return topLevelNames;
    }

    /// <summary>Gets nested owned container attribute names for a root entity type.</summary>
    internal static HashSet<string> GetNestedOwnedContainingAttributeNames(IEntityType entityType)
    {
        HashSet<string> nestedNames = new(StringComparer.Ordinal);
        if (!IsRootEntity(entityType))
            return nestedNames;

        foreach (var projectionEntityType in GetProjectionEntityTypes(entityType))
            foreach (var navigation in projectionEntityType.GetDeclaredNavigations())
            {
                if (!navigation.IsEmbedded() || !navigation.TargetEntityType.IsOwned())
                    continue;

                CollectNestedOwnedContainingAttributeNames(
                    navigation.TargetEntityType,
                    nestedNames);
            }

        return nestedNames;
    }

    /// <summary>Determines whether a property should be projected as a top-level DynamoDB attribute.</summary>
    internal static bool ShouldProjectTopLevelProperty(
        IEntityType entityType,
        IProperty property,
        HashSet<string> topLevelOwnedContainingAttributeNames)
    {
        if (!IsRootEntity(entityType))
            return false;

        if (property.IsOwnedOrdinalKeyProperty())
            return false;

        // Runtime-only metadata is sourced from query/runtime context rather than item attributes,
        // so it must not be added to SELECT projections.
        if (property.IsRuntimeOnly())
            return false;

        if (!property.IsShadowProperty())
            return property.GetTypeMapping() != null;

        // Shadow properties that represent owned entity containers (e.g. navigation attribute
        // names)
        // are projected so downstream materialization can navigate into the nested map/list.
        if (topLevelOwnedContainingAttributeNames.Contains(property.Name))
            return true;

        // Scalar shadow properties with a DynamoDB type mapping must be projected so the
        // compiled shaper can materialize them into the shadow snapshot.
        return property.GetTypeMapping() is DynamoTypeMapping;
    }

    /// <summary>Gets the scalar properties that should be considered for top-level projection expansion.</summary>
    /// <remarks>
    ///     For inheritance roots that can materialize multiple concrete types, this returns declared
    ///     properties across the relevant hierarchy so derived-type materialization has access to derived
    ///     attributes.
    /// </remarks>
    internal static IEnumerable<IProperty> GetTopLevelProjectionProperties(IEntityType entityType)
    {
        if (!ShouldProjectHierarchyProperties(entityType))
            return entityType.GetProperties();

        HashSet<IProperty> seen = [];

        return GetProjectionEntityTypes(entityType)
            .SelectMany(
                projectionEntityType => projectionEntityType.GetDeclaredProperties(),
                (projectionEntityType, property) => new { projectionEntityType, property })
            .Where(t => seen.Add(t.property))
            .Select(t => t.property)
            .ToList();
    }

    /// <summary>Determines whether hierarchy-wide projection is required for inheritance materialization.</summary>
    private static bool ShouldProjectHierarchyProperties(IEntityType entityType)
        => IsRootEntity(entityType) && entityType.GetConcreteDerivedTypesInclusive().Count() > 1;

    /// <summary>Gets ordered entity types participating in projection for an inheritance root query.</summary>
    private static IEnumerable<IEntityType> GetProjectionEntityTypes(IEntityType entityType)
    {
        if (!ShouldProjectHierarchyProperties(entityType))
            return [entityType];

        HashSet<IEntityType> seen = [];
        List<IEntityType> ordered = [];

        foreach (var baseType in entityType.GetAllBaseTypes().Reverse())
            if (seen.Add(baseType))
                ordered.Add(baseType);

        if (seen.Add(entityType))
            ordered.Add(entityType);

        foreach (var derivedType in entityType
            .GetDerivedTypesInclusive()
            .Where(static type => !type.IsAbstract())
            .Where(type => !Equals(type, entityType))
            .OrderBy(static type => type.Name, StringComparer.Ordinal))
            if (seen.Add(derivedType))
                ordered.Add(derivedType);

        return ordered;
    }

    /// <summary>Collects containing-attribute names for nested owned navigations under an owned entity.</summary>
    private static void CollectNestedOwnedContainingAttributeNames(
        IEntityType ownedEntityType,
        HashSet<string> nestedNames)
    {
        foreach (var navigation in ownedEntityType.GetNavigations())
        {
            if (!navigation.IsEmbedded() || !navigation.TargetEntityType.IsOwned())
                continue;

            var containingAttributeName =
                navigation.TargetEntityType.GetContainingAttributeName() ?? navigation.Name;
            nestedNames.Add(containingAttributeName);
            CollectNestedOwnedContainingAttributeNames(navigation.TargetEntityType, nestedNames);
        }
    }
}
