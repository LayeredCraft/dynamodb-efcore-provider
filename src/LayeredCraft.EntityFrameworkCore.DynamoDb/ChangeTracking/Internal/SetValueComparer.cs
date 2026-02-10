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

        var leftList = left.ToList();
        var rightList = right.ToList();

        if (leftList.Count != rightList.Count)
            return false;

        foreach (var leftElement in leftList)
        {
            var matchFound = false;
            foreach (var rightElement in rightList)
            {
                if (!elementComparer.Equals(leftElement, rightElement))
                    continue;

                matchFound = true;
                break;
            }

            if (!matchFound)
                return false;
        }

        return true;
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
        var snapshot = new HashSet<TElement>();
        foreach (var element in source)
            snapshot.Add(element is null ? element! : elementComparer.Snapshot(element));

        return snapshot;
    }
}
