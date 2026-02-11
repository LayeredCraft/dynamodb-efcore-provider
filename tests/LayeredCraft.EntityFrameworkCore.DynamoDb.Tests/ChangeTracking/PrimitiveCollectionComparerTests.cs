using System.Collections;
using LayeredCraft.EntityFrameworkCore.DynamoDb.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.ChangeTracking;

public class PrimitiveCollectionComparerTests
{
    [Fact]
    public void SetComparer_Snapshot_CustomConcreteSet_DoesNotThrow_AndPreservesType()
    {
        var comparer =
            new SetValueComparer<SortedSet<string>, string>(new ValueComparer<string>(false));
        var source = new SortedSet<string>(StringComparer.Ordinal) { "alpha", "beta" };

        var snapshot = comparer.Snapshot(source);

        snapshot.Should().BeOfType<SortedSet<string>>();
        comparer.Equals(source, snapshot).Should().BeTrue();

        source.Add("gamma");
        comparer.Equals(source, snapshot).Should().BeFalse();
    }

    [Fact]
    public void StringDictionaryComparer_Snapshot_MutableOnlyDictionary_DoesNotThrow()
    {
        var comparer = new StringDictionaryValueComparer<MutableOnlyDictionary<int>, int>(
            new ValueComparer<int>(false),
            false);

        var source = new MutableOnlyDictionary<int> { ["math"] = 10, ["science"] = 20 };

        var snapshot = comparer.Snapshot(source);

        snapshot.Should().BeOfType<MutableOnlyDictionary<int>>();
        comparer.Equals(source, snapshot).Should().BeTrue();

        source["math"] = 99;
        comparer.Equals(source, snapshot).Should().BeFalse();
    }

    [Fact]
    public void NullableStringDictionaryComparer_Snapshot_MutableOnlyDictionary_DoesNotThrow()
    {
        var comparer = new NullableStringDictionaryValueComparer<MutableOnlyDictionary<int?>, int>(
            new ValueComparer<int>(false),
            false);

        var source = new MutableOnlyDictionary<int?> { ["math"] = 10, ["science"] = null };

        var snapshot = comparer.Snapshot(source);

        snapshot.Should().BeOfType<MutableOnlyDictionary<int?>>();
        comparer.Equals(source, snapshot).Should().BeTrue();

        source["science"] = 42;
        comparer.Equals(source, snapshot).Should().BeFalse();
    }

    [Fact]
    public void StringDictionaryComparer_EqualValues_HaveSameHashCode_WhenInsertionOrderDiffers()
    {
        var comparer = new StringDictionaryValueComparer<MutableOnlyDictionary<int>, int>(
            new ValueComparer<int>(false),
            false);

        var left = new MutableOnlyDictionary<int> { ["a"] = 1, ["b"] = 2, ["c"] = 3 };

        var right = new MutableOnlyDictionary<int> { ["c"] = 3, ["b"] = 2, ["a"] = 1 };

        comparer.Equals(left, right).Should().BeTrue();
        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
    }

    private sealed class MutableOnlyDictionary<TValue> : IDictionary<string, TValue>
    {
        private readonly Dictionary<string, TValue> _inner = new(StringComparer.Ordinal);

        public TValue this[string key]
        {
            get => _inner[key];
            set => _inner[key] = value;
        }

        public ICollection<string> Keys => _inner.Keys;

        public ICollection<TValue> Values => _inner.Values;

        public int Count => _inner.Count;

        public bool IsReadOnly => false;

        public void Add(string key, TValue value) => _inner.Add(key, value);

        public void Add(KeyValuePair<string, TValue> item)
            => ((IDictionary<string, TValue>)_inner).Add(item);

        public void Clear() => _inner.Clear();

        public bool Contains(KeyValuePair<string, TValue> item)
            => ((IDictionary<string, TValue>)_inner).Contains(item);

        public bool ContainsKey(string key) => _inner.ContainsKey(key);

        public void CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex)
            => ((IDictionary<string, TValue>)_inner).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator() => _inner.GetEnumerator();

        public bool Remove(string key) => _inner.Remove(key);

        public bool Remove(KeyValuePair<string, TValue> item)
            => ((IDictionary<string, TValue>)_inner).Remove(item);

        public bool TryGetValue(string key, out TValue value)
            => _inner.TryGetValue(key, out value!);

        IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }
}
