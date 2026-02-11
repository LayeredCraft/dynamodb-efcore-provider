using System.Collections;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.ChangeTracking.Internal;

internal sealed class NullableStringDictionaryValueComparer<TDictionary, TValue>(
    ValueComparer elementComparer,
    bool readOnly) : ValueComparer<TDictionary>(
    (left, right) => Equals(left, right, elementComparer),
    value => GetHashCode(value, elementComparer),
    value => Snapshot(value, elementComparer, readOnly))
    where TDictionary : class, IEnumerable<KeyValuePair<string, TValue?>> where TValue : struct
{
    /// <summary>Compares two nullable-value dictionaries by key and value.</summary>
    private static bool Equals(TDictionary? left, TDictionary? right, ValueComparer elementComparer)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        var leftDictionary = ToReadOnlyDictionary(left);
        var rightDictionary = ToReadOnlyDictionary(right);

        if (leftDictionary.Count != rightDictionary.Count)
            return false;

        foreach (var pair in leftDictionary)
        {
            if (!rightDictionary.TryGetValue(pair.Key, out var rightValue))
                return false;

            if (pair.Value.HasValue != rightValue.HasValue)
                return false;

            if (pair.Value.HasValue && !elementComparer.Equals(pair.Value.Value, rightValue!.Value))
                return false;
        }

        return true;
    }

    /// <summary>Produces an order-insensitive hash for dictionary contents.</summary>
    private static int GetHashCode(TDictionary? value, ValueComparer elementComparer)
    {
        if (value is null)
            return 0;

        var dictionary = ToReadOnlyDictionary(value);
        var hash = dictionary.Count;
        foreach (var pair in dictionary)
        {
            var valueHash = pair.Value.HasValue ? elementComparer.GetHashCode(pair.Value.Value) : 0;

            hash ^= HashCode.Combine(pair.Key, valueHash);
        }

        return hash;
    }

    /// <summary>Creates a snapshot dictionary, or returns the source for read-only mappings.</summary>
    private static TDictionary Snapshot(
        TDictionary source,
        ValueComparer elementComparer,
        bool readOnly)
    {
        if (readOnly)
            return source;

        var snapshot = new Dictionary<string, TValue?>(StringComparer.Ordinal);
        foreach (var pair in source)
            snapshot[pair.Key] = pair.Value.HasValue
                ? (TValue)elementComparer.Snapshot(pair.Value.Value)!
                : null;

        return (TDictionary)(object)snapshot;
    }

    /// <summary>Adapts supported dictionary runtime shapes to a single read-only view.</summary>
    private static IReadOnlyDictionary<string, TValue?> ToReadOnlyDictionary(TDictionary dictionary)
        => dictionary switch
        {
            IReadOnlyDictionary<string, TValue?> readOnlyDictionary => readOnlyDictionary,
            IDictionary<string, TValue?> mutableDictionary => new DictionaryAdapter(
                mutableDictionary),
            _ => throw new InvalidOperationException(
                $"Expected {typeof(TDictionary).Name} to implement IDictionary<string, {typeof(TValue).Name}?> "
                + "or IReadOnlyDictionary<string, TValue?>."),
        };

    private sealed class DictionaryAdapter(IDictionary<string, TValue?> dictionary)
        : IReadOnlyDictionary<string, TValue?>
    {
        /// <summary>Returns an enumerator over key-value pairs.</summary>
        public IEnumerator<KeyValuePair<string, TValue?>> GetEnumerator()
            => dictionary.GetEnumerator();

        /// <summary>Returns a non-generic enumerator.</summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Gets the number of entries.</summary>
        public int Count => dictionary.Count;

        /// <summary>Checks whether a key exists.</summary>
        public bool ContainsKey(string key) => dictionary.ContainsKey(key);

        /// <summary>Looks up a value by key.</summary>
        public bool TryGetValue(string key, out TValue? value)
            => dictionary.TryGetValue(key, out value);

        /// <summary>Gets the value for a key.</summary>
        public TValue? this[string key] => dictionary[key];

        /// <summary>Gets all keys.</summary>
        public IEnumerable<string> Keys => dictionary.Keys;

        /// <summary>Gets all values.</summary>
        public IEnumerable<TValue?> Values => dictionary.Values;
    }
}
