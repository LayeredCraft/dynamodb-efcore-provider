using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.ChangeTracking.Internal;

internal sealed class StringDictionaryValueComparer<TDictionary, TValue>(
    ValueComparer elementComparer,
    bool readOnly)
    : ValueComparer<TDictionary>(
        (left, right) => Compare(left, right, (ValueComparer<TValue>)elementComparer),
        source => GetHashCode(source, (ValueComparer<TValue>)elementComparer),
        source => Snapshot(source, (ValueComparer<TValue>)elementComparer, readOnly))
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

        if (!TryGetDictionaryAccess(left, out var leftDictionary)
            || !TryGetDictionaryAccess(right, out var rightDictionary))
            return false;

        if (leftDictionary.Count != rightDictionary.Count)
            return false;

        foreach (var (key, leftValue) in leftDictionary.Entries)
            if (!rightDictionary.TryGetValue(key, out var rightValue)
                || !elementComparer.Equals(leftValue, rightValue))
                return false;

        return true;
    }

    private static int GetHashCode(TDictionary source, ValueComparer<TValue> elementComparer)
    {
        if (!TryGetDictionaryAccess(source, out var dictionary))
            return 0;

        var hash = dictionary.Count;
        foreach (var (key, value) in dictionary.Entries)
            hash ^= HashCode.Combine(key, value is null ? 0 : elementComparer.GetHashCode(value));

        return hash;
    }

    private static TDictionary Snapshot(
        TDictionary source,
        ValueComparer<TValue> elementComparer,
        bool readOnly)
    {
        if (!TryGetDictionaryAccess(source, out var dictionary))
            throw new InvalidOperationException(
                $"Dictionary comparer requires IDictionary<string, {typeof(TValue).Name}> "
                + $"or IReadOnlyDictionary<string, {typeof(TValue).Name}>.");

        if (readOnly)
            return source;

        var snapshot = CreateMutableDictionary(source, dictionary.Count);
        foreach (var (key, value) in dictionary.Entries)
            snapshot[key] = value is null ? value! : elementComparer.Snapshot(value);

        return (TDictionary)snapshot;
    }

    private static IDictionary<string, TValue> CreateMutableDictionary(
        TDictionary source,
        int capacity)
    {
        if (source is IDictionary<string, TValue> typedDictionary)
        {
            var runtimeType = typedDictionary.GetType();
            if (!runtimeType.IsAbstract
                && runtimeType.GetConstructor(Type.EmptyTypes) is { IsPublic: true })
                return (IDictionary<string, TValue>)Activator.CreateInstance(runtimeType)!;
        }

        return new Dictionary<string, TValue>(capacity, StringComparer.Ordinal);
    }

    private static bool TryGetDictionaryAccess(TDictionary source, out DictionaryAccess dictionary)
    {
        if (source is IReadOnlyDictionary<string, TValue> readOnlyDictionary)
        {
            dictionary = new DictionaryAccess(readOnlyDictionary, null);
            return true;
        }

        if (source is IDictionary<string, TValue> mutableDictionary)
        {
            dictionary = new DictionaryAccess(null, mutableDictionary);
            return true;
        }

        dictionary = default;
        return false;
    }

    private readonly record struct DictionaryAccess(
        IReadOnlyDictionary<string, TValue>? ReadOnly,
        IDictionary<string, TValue>? Mutable)
    {
        public int Count => ReadOnly?.Count ?? Mutable!.Count;

        public IEnumerable<KeyValuePair<string, TValue>> Entries
            => ReadOnly ?? (IEnumerable<KeyValuePair<string, TValue>>)Mutable!;

        public bool TryGetValue(string key, out TValue value)
        {
            if (ReadOnly is not null)
                return ReadOnly.TryGetValue(key, out value!);

            return Mutable!.TryGetValue(key, out value!);
        }
    }
}
