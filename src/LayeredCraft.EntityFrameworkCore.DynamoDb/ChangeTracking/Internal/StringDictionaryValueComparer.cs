using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.ChangeTracking.Internal;

internal sealed class StringDictionaryValueComparer<TDictionary, TValue>(
    ValueComparer elementComparer,
    bool readOnly)
    : ValueComparer<TDictionary>(
        (left, right) => Compare(left, right, (ValueComparer<TValue>)elementComparer),
        source => GetHashCode(source, (ValueComparer<TValue>)elementComparer),
        source => (TDictionary)Snapshot(source, (ValueComparer<TValue>)elementComparer, readOnly))
    where TDictionary : class, IEnumerable<KeyValuePair<string, TValue>>
{
    private static bool Compare(
        TDictionary? left,
        TDictionary? right,
        ValueComparer<TValue> elementComparer)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        if (left is not IReadOnlyDictionary<string, TValue> leftDictionary
            || right is not IReadOnlyDictionary<string, TValue> rightDictionary)
            return false;

        if (leftDictionary.Count != rightDictionary.Count)
            return false;

        foreach (var (key, leftValue) in leftDictionary)
            if (!rightDictionary.TryGetValue(key, out var rightValue)
                || !elementComparer.Equals(leftValue, rightValue))
                return false;

        return true;
    }

    private static int GetHashCode(TDictionary source, ValueComparer<TValue> elementComparer)
    {
        var hashCode = new HashCode();
        foreach (var (key, value) in source)
        {
            hashCode.Add(key);
            hashCode.Add(value is null ? 0 : elementComparer.GetHashCode(value));
        }

        return hashCode.ToHashCode();
    }

    private static TDictionary Snapshot(
        TDictionary source,
        ValueComparer<TValue> elementComparer,
        bool readOnly)
    {
        if (source is not IReadOnlyDictionary<string, TValue> dictionary)
            throw new InvalidOperationException(
                $"Dictionary comparer requires IReadOnlyDictionary<string, {typeof(TValue).Name}>.");

        if (readOnly)
            return source;

        var snapshot = new Dictionary<string, TValue>(dictionary.Count, StringComparer.Ordinal);
        foreach (var (key, value) in dictionary)
            snapshot[key] = value is null ? value! : elementComparer.Snapshot(value);

        return (TDictionary)(object)snapshot;
    }
}
