using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Metadata;

#pragma warning disable EF1001 // Internal EF Core API: InternalEntityEntry

namespace EntityFrameworkCore.DynamoDb.Storage;

internal static class DynamoEntryGraph
{
    internal static InternalEntityEntry GetRootEntry(InternalEntityEntry entry)
    {
        var cache = new Dictionary<InternalEntityEntry, InternalEntityEntry>(
            ReferenceEqualityComparer.Instance);
        return GetRootEntry(entry, cache);
    }

    internal static InternalEntityEntry GetRootEntry(
        InternalEntityEntry entry,
        Dictionary<InternalEntityEntry, InternalEntityEntry> cache)
    {
        if (cache.TryGetValue(entry, out var cachedRoot))
            return cachedRoot;

        var current = entry;
        List<InternalEntityEntry>? visited = null;

        while (true)
        {
            if (cache.TryGetValue(current, out cachedRoot))
            {
                current = cachedRoot;
                break;
            }

            visited ??= [];
            visited.Add(current);

            if (!current.EntityType.IsOwned())
                break;

            var ownership = current.EntityType.FindOwnership()
                ?? throw new InvalidOperationException(
                    $"Owned entity type '{current.EntityType.DisplayName()}' has no ownership metadata.");

            var principal = current.StateManager.FindPrincipal(current, ownership)
                ?? throw new InvalidOperationException(
                    $"Owned entity '{current.EntityType.DisplayName()}' is orphaned from its principal "
                    + $"'{ownership.PrincipalEntityType.DisplayName()}'.");

            current = principal;
        }

        if (visited is null)
            return current;

        foreach (var node in visited)
            cache[node] = current;

        return current;
    }

    internal static bool TryVisitOwnershipChain(
        InternalEntityEntry ownedEntry,
        Action<InternalEntityEntry, INavigation> visit)
    {
        var current = ownedEntry;

        while (current.EntityType.IsOwned())
        {
            var ownership = current.EntityType.FindOwnership();
            if (ownership is null)
                return false;

            var principal = current.StateManager.FindPrincipal(current, ownership);
            if (principal is null)
                return false;

            var nav = ownership.PrincipalToDependent;
            if (nav is null)
                return false;

            visit(principal, nav);
            current = principal;
        }

        return true;
    }
}
