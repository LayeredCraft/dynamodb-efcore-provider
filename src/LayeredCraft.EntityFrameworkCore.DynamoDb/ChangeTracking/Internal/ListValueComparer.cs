using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.ChangeTracking.Internal;

internal sealed class ListValueComparer<TList, TElement>(ValueComparer elementComparer)
    : ValueComparer<TList>(
        (left, right) => Equals(left, right, elementComparer),
        value => GetHashCode(value, elementComparer),
        value => Snapshot(value, elementComparer)) where TList : class, IEnumerable<TElement>
{
    /// <summary>Compares two sequences using ordered element equality.</summary>
    private static bool Equals(TList? left, TList? right, ValueComparer elementComparer)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        using var leftEnumerator = left.GetEnumerator();
        using var rightEnumerator = right.GetEnumerator();
        while (leftEnumerator.MoveNext())
            if (!rightEnumerator.MoveNext()
                || !elementComparer.Equals(leftEnumerator.Current, rightEnumerator.Current))
                return false;

        return !rightEnumerator.MoveNext();
    }

    /// <summary>Produces an order-sensitive hash for a sequence.</summary>
    private static int GetHashCode(TList? value, ValueComparer elementComparer)
    {
        if (value is null)
            return 0;

        var hash = new HashCode();
        foreach (var element in value)
            hash.Add(elementComparer.GetHashCode(element!));

        return hash.ToHashCode();
    }

    /// <summary>Creates a snapshot as an array for array shapes; otherwise as <see cref="List{T}" />.</summary>
    private static TList Snapshot(TList source, ValueComparer elementComparer)
    {
        var snapshotValues = source
            .Select(element => (TElement)elementComparer.Snapshot(element!)!)
            .ToList();

        if (typeof(TList).IsArray)
        {
            var array = snapshotValues.ToArray();
            return (TList)(object)array;
        }

        return (TList)(object)snapshotValues;
    }
}
