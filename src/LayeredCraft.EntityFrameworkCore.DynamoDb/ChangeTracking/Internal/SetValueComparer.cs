using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.ChangeTracking.Internal;

internal sealed class SetValueComparer<TSet, TElement>(ValueComparer elementComparer)
    : ValueComparer<TSet>(
        (left, right) => Compare(left, right, (ValueComparer<TElement>)elementComparer),
        source => GetHashCode(source, (ValueComparer<TElement>)elementComparer),
        source => (TSet)(object)Snapshot(source, (ValueComparer<TElement>)elementComparer))
    where TSet : class, IEnumerable<TElement>
{
    private static bool Compare(TSet? left, TSet? right, ValueComparer<TElement> elementComparer)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        var rightSet =
            new HashSet<TElement>(right, new ValueComparerEqualityComparer(elementComparer));
        foreach (var leftElement in left)
        {
            if (!rightSet.Remove(leftElement))
                return false;
        }

        return rightSet.Count == 0;
    }

    private static int GetHashCode(TSet source, ValueComparer<TElement> elementComparer)
    {
        var hash = 0;
        foreach (var element in source)
            hash ^= element is null ? 0 : elementComparer.GetHashCode(element);

        return hash;
    }

    private static HashSet<TElement> Snapshot(TSet source, ValueComparer<TElement> elementComparer)
    {
        var snapshot =
            source is HashSet<TElement> sourceHashSet
                ? new HashSet<TElement>(sourceHashSet.Comparer)
                : new HashSet<TElement>();

        foreach (var element in source)
            snapshot.Add(element is null ? element! : elementComparer.Snapshot(element));

        return snapshot;
    }

    private sealed class ValueComparerEqualityComparer(ValueComparer<TElement> comparer)
        : IEqualityComparer<TElement>
    {
        public bool Equals(TElement? x, TElement? y) => comparer.Equals(x, y);

        public int GetHashCode(TElement obj) => obj is null ? 0 : comparer.GetHashCode(obj);
    }
}
