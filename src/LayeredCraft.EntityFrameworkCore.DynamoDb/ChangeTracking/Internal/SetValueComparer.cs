using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.ChangeTracking.Internal;

internal sealed class SetValueComparer<TSet, TElement>(ValueComparer elementComparer)
    : ValueComparer<TSet>(
        (left, right) => Compare(left, right, (ValueComparer<TElement>)elementComparer),
        source => GetHashCode(source, (ValueComparer<TElement>)elementComparer),
        source => Snapshot(source, (ValueComparer<TElement>)elementComparer))
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

    private static TSet Snapshot(TSet source, ValueComparer<TElement> elementComparer)
    {
        var snapshot = CreateMutableSet(source);

        foreach (var element in source)
            snapshot.Add(element is null ? element! : elementComparer.Snapshot(element));

        if (snapshot is TSet typedSnapshot)
            return typedSnapshot;

        throw new InvalidOperationException(
            $"Unable to snapshot set type '{typeof(TSet)}'. "
            + "Ensure the type is assignable from HashSet<T> or provides a public parameterless constructor.");
    }

    private static ISet<TElement> CreateMutableSet(TSet source)
    {
        if (source is HashSet<TElement> sourceHashSet
            && sourceHashSet.GetType() == typeof(HashSet<TElement>))
            return new HashSet<TElement>(sourceHashSet.Comparer);

        if (source is ISet<TElement> typedSet)
        {
            var runtimeType = typedSet.GetType();
            if (!runtimeType.IsAbstract
                && runtimeType.GetConstructor(Type.EmptyTypes) is { IsPublic: true })
                return (ISet<TElement>)Activator.CreateInstance(runtimeType)!;
        }

        return new HashSet<TElement>();
    }

    private sealed class ValueComparerEqualityComparer(ValueComparer<TElement> comparer)
        : IEqualityComparer<TElement>
    {
        public bool Equals(TElement? x, TElement? y) => comparer.Equals(x, y);

        public int GetHashCode(TElement obj) => obj is null ? 0 : comparer.GetHashCode(obj);
    }
}
