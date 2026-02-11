using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.ChangeTracking.Internal;

internal sealed class SetValueComparer<TSet, TElement>(ValueComparer elementComparer)
    : ValueComparer<TSet>(
        (left, right) => Equals(left, right, elementComparer),
        value => GetHashCode(value, elementComparer),
        value => Snapshot(value, elementComparer)) where TSet : class, IEnumerable<TElement>
{
    /// <summary>Compares two sets using element equality without considering order.</summary>
    private static bool Equals(TSet? left, TSet? right, ValueComparer elementComparer)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        var rightSet = new HashSet<TElement>(new ElementComparerAdapter(elementComparer));
        foreach (var element in right)
            rightSet.Add(element);

        var rightCount = rightSet.Count;

        var leftCount = 0;
        foreach (var element in left)
        {
            leftCount++;
            if (!rightSet.Remove(element))
                return false;
        }

        return rightSet.Count == 0 && leftCount == rightCount;
    }

    /// <summary>Produces an order-insensitive hash for a set.</summary>
    private static int GetHashCode(TSet? value, ValueComparer elementComparer)
    {
        if (value is null)
            return 0;

        var hash = 0;
        foreach (var element in value)
            hash ^= elementComparer.GetHashCode(element!);

        return hash;
    }

    /// <summary>Creates a snapshot as <see cref="HashSet{T}" /> and preserves comparer when possible.</summary>
    private static TSet Snapshot(TSet value, ValueComparer elementComparer)
    {
        var snapshot = value as HashSet<TElement> is { } sourceHashSet
            ? new HashSet<TElement>(sourceHashSet.Comparer)
            : new HashSet<TElement>();

        foreach (var element in value)
            snapshot.Add((TElement)elementComparer.Snapshot(element!)!);

        return (TSet)(object)snapshot;
    }

    private sealed class ElementComparerAdapter(ValueComparer elementComparer)
        : IEqualityComparer<TElement>
    {
        /// <summary>Delegates element equality to the configured EF comparer.</summary>
        public bool Equals(TElement? x, TElement? y) => elementComparer.Equals(x, y);

        /// <summary>Delegates element hashing to the configured EF comparer.</summary>
        public int GetHashCode(TElement obj) => elementComparer.GetHashCode(obj!);
    }
}
