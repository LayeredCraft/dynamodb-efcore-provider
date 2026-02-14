using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

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

        foreach (var navigation in entityType.GetNavigations())
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

        foreach (var navigation in entityType.GetNavigations())
        {
            if (!navigation.IsEmbedded() || !navigation.TargetEntityType.IsOwned())
                continue;

            CollectNestedOwnedContainingAttributeNames(navigation.TargetEntityType, nestedNames);
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

        if (!Equals(property.DeclaringType, entityType))
            return topLevelOwnedContainingAttributeNames.Contains(property.Name);

        if (!property.IsShadowProperty())
            return property.GetTypeMapping() != null;

        return topLevelOwnedContainingAttributeNames.Contains(property.Name);
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
