using EntityFrameworkCore.DynamoDb.Storage;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

public class DynamoWireValueConversionTests
{
    [Fact]
    public void ConvertProviderValueToAttributeValue_ByteArray_WritesBinaryMember()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };

        var attributeValue = DynamoWireValueConversion.ConvertProviderValueToAttributeValue(bytes);

        attributeValue.B.Should().NotBeNull();
        attributeValue.B.ToArray().Should().Equal(bytes);
        attributeValue.S.Should().BeNull();
        attributeValue.N.Should().BeNull();
    }

    [Fact]
    public void SerializeSet_ByteArrayValues_WritesBinarySet()
    {
        var first = new byte[] { 1, 2 };
        var second = new byte[] { 3, 4 };
        var values = new HashSet<byte[]>([first, second], ByteArrayComparer.Instance);

        var attributeValue = DynamoAttributeValueCollectionHelpers.SerializeSet(values);

        attributeValue.BS.Should().NotBeNull();
        attributeValue.BS.Should().HaveCount(2);
        attributeValue
            .BS
            .Select(static stream => stream.ToArray())
            .Should()
            .BeEquivalentTo([first, second]);
    }

    [Fact]
    public void SerializeList_WithConverter_UsesConvertedProviderValues()
    {
        var values = new List<DateTimeOffset>
        {
            new(2026, 4, 1, 10, 30, 0, TimeSpan.Zero),
            new(2026, 4, 2, 11, 45, 0, TimeSpan.Zero),
        };

        var attributeValue = DynamoAttributeValueCollectionHelpers.SerializeList(
            values,
            static value => value.ToUnixTimeSeconds().ToString());

        attributeValue.L.Should().HaveCount(2);
        attributeValue.L[0].S.Should().Be(values[0].ToUnixTimeSeconds().ToString());
        attributeValue.L[1].S.Should().Be(values[1].ToUnixTimeSeconds().ToString());
    }

    [Fact]
    public void SerializeDictionary_WithConverter_UsesConvertedProviderValues()
    {
        var values = new Dictionary<string, Guid>
        {
            ["first"] = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ["second"] = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        };

        var attributeValue = DynamoAttributeValueCollectionHelpers.SerializeDictionary(
            values,
            static value => value.ToString("N"));

        attributeValue.M["first"].S.Should().Be("11111111111111111111111111111111");
        attributeValue.M["second"].S.Should().Be("22222222222222222222222222222222");
    }

    [Fact]
    public void SerializeList_WithConverterReturningNull_WritesNullElement()
    {
        var attributeValue = DynamoAttributeValueCollectionHelpers.SerializeList(
            new List<string> { "keep", "drop" },
            static value => value == "drop" ? null : value.ToUpperInvariant());

        attributeValue.L.Should().HaveCount(2);
        attributeValue.L[0].S.Should().Be("KEEP");
        attributeValue.L[1].NULL.Should().BeTrue();
    }

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static ByteArrayComparer Instance { get; } = new();

        public bool Equals(byte[]? x, byte[]? y)
            => ReferenceEquals(x, y)
                || (x is not null && y is not null && x.AsSpan().SequenceEqual(y));

        public int GetHashCode(byte[] obj)
        {
            var hash = new HashCode();
            foreach (var b in obj)
                hash.Add(b);
            return hash.ToHashCode();
        }
    }
}
