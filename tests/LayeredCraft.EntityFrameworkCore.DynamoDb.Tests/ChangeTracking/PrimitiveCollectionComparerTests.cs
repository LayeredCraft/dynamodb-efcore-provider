using LayeredCraft.EntityFrameworkCore.DynamoDb.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.ChangeTracking;

public class PrimitiveCollectionComparerTests
{
    [Fact]
    public void SetComparer_Snapshot_InterfaceSet_ReturnsHashSet_AndPreservesComparer()
    {
        var elementComparer = ValueComparer.CreateDefault(typeof(string), false);
        var comparer = new SetValueComparer<ISet<string>, string>(elementComparer);

        ISet<string> source =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "alpha", "beta" };

        var snapshot = comparer.Snapshot(source);

        snapshot.Should().BeOfType<HashSet<string>>();
        ((HashSet<string>)snapshot).Comparer.Should().BeSameAs(StringComparer.OrdinalIgnoreCase);
        comparer.Equals(source, snapshot).Should().BeTrue();
    }

    [Fact]
    public void DictionaryComparer_Snapshot_InterfaceDictionary_ReturnsDictionary()
    {
        var elementComparer = ValueComparer.CreateDefault(typeof(int), false);
        var comparer = new StringDictionaryValueComparer<IDictionary<string, int>, int>(
            elementComparer,
            false);

        IDictionary<string, int> source = new Dictionary<string, int>
        {
            ["math"] = 10, ["science"] = 12,
        };

        var snapshot = comparer.Snapshot(source);

        snapshot.Should().BeOfType<Dictionary<string, int>>();
        snapshot.Should().NotBeSameAs(source);
        comparer.Equals(source, snapshot).Should().BeTrue();
    }

    [Fact]
    public void NullableDictionaryComparer_Snapshot_ReadOnlyDictionary_ReturnsDictionary()
    {
        var elementComparer = ValueComparer.CreateDefault(typeof(int), false);
        var comparer =
            new NullableStringDictionaryValueComparer<IReadOnlyDictionary<string, int?>, int>(
                elementComparer,
                false);

        IReadOnlyDictionary<string, int?> source =
            new Dictionary<string, int?> { ["a"] = 1, ["b"] = null };

        var snapshot = comparer.Snapshot(source);

        snapshot.Should().BeOfType<Dictionary<string, int?>>();
        snapshot.Should().NotBeSameAs(source);
        comparer.Equals(source, snapshot).Should().BeTrue();
    }

    [Fact]
    public void DictionaryComparer_EqualValues_HaveSameHashCode_WhenInsertionOrderDiffers()
    {
        var elementComparer = ValueComparer.CreateDefault(typeof(int), false);
        var comparer = new StringDictionaryValueComparer<IDictionary<string, int>, int>(
            elementComparer,
            false);

        IDictionary<string, int> left = new Dictionary<string, int>
        {
            ["x"] = 1, ["y"] = 2, ["z"] = 3,
        };

        IDictionary<string, int> right = new Dictionary<string, int>
        {
            ["z"] = 3, ["x"] = 1, ["y"] = 2,
        };

        comparer.Equals(left, right).Should().BeTrue();
        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
    }
}
