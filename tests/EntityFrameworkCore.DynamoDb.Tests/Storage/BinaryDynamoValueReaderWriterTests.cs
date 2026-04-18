using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Storage;
using EntityFrameworkCore.DynamoDb.Storage.Internal;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

public class BinaryDynamoValueReaderWriterTests
{
    private readonly BinaryDynamoValueReaderWriter _sut = new();

    [Fact]
    public void Write_ByteArray_WrapsInNonWritableMemoryStream()
    {
        var bytes = new byte[] { 1, 2, 3 };

        var av = _sut.Write(bytes);

        av.B.Should().NotBeNull();
        av.B.ToArray().Should().Equal(bytes);
        // All other members must be unset
        av.S.Should().BeNull();
        av.N.Should().BeNull();
        av.BOOL.Should().BeNull();
    }

    [Fact]
    public void Write_EmptyByteArray_WritesEmptyBinaryMember()
    {
        var av = _sut.Write([]);

        av.B.Should().NotBeNull();
        av.B.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void Read_AttributeValueWithB_ReturnsByteArray()
    {
        var bytes = new byte[] { 4, 5, 6 };
        var av = new AttributeValue { B = new MemoryStream(bytes, false) };

        var result = _sut.Read(av, "payload", required: true, property: null);

        result.Should().Equal(bytes);
    }

    [Fact]
    public void Read_RequiredMissingB_ThrowsInvalidOperationException()
    {
        var av = new AttributeValue { S = "not binary" };

        var act = () => _sut.Read(av, "payload", required: true, property: null);

        act.Should().Throw<InvalidOperationException>().WithMessage("*'payload'*");
    }

    [Fact]
    public void Read_OptionalMissingB_ReturnsNull()
    {
        var av = new AttributeValue { S = "not binary" };

        var result = _sut.Read(av, "payload", required: false, property: null);

        result.Should().BeNull();
    }

    [Fact]
    public void ToPartiQlLiteral_Throws_NotSupportedException()
    {
        var act = () => _sut.ToPartiQlLiteral(new byte[] { 1 });

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ConvertProviderValueToAttributeValue_NullByteArray_WritesNullMember()
    {
        var av = DynamoWireValueConversion.ConvertProviderValueToAttributeValue<byte[]?>(null);

        av.NULL.Should().BeTrue();
        av.B.Should().BeNull();
    }

    [Fact]
    public void WireMemberName_IsBinaryMember()
    {
        _sut.WireMemberName.Should().Be(nameof(AttributeValue.B));
    }
}
