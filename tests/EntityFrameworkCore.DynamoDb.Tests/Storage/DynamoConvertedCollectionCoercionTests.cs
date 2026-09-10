using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Storage.Internal;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

/// <summary>
///     Verifies that <see cref="DynamoValueReaderWriterFactory.CoerceReaderWriter{TValue}" />
///     resolves the non-generic NativeAOT converted wrapper into a typed codec for primitive
///     collection element mappings. Under JIT the factory composes typed wrappers, so the AOT
///     wrapper is constructed directly here to exercise the coercion contract.
/// </summary>
public class DynamoConvertedCollectionCoercionTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CoerceReaderWriter_AotConvertedWrapper_RoundTripsConvertedElement()
    {
        var coerced = DynamoValueReaderWriterFactory.CoerceReaderWriter<Guid>(
            CreateAotWrapper(new GuidToStringConverter()));

        var attributeValue = coerced.Write(new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"));

        attributeValue.S.Should().Be("0f8fad5b-d9cb-469f-a165-70867728950e");
        coerced
            .ToPartiQlLiteral(new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"))
            .Should()
            .Be("'0f8fad5b-d9cb-469f-a165-70867728950e'");
        coerced
            .ReadObject(
                new AttributeValue { S = "0f8fad5b-d9cb-469f-a165-70867728950e" },
                "Path",
                true,
                null)
            .Should()
            .Be(new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CoerceReaderWriter_AotConvertedWrapper_SupportsNullableElement()
    {
        var coerced = DynamoValueReaderWriterFactory.CoerceReaderWriter<Guid?>(
            CreateAotWrapper(new GuidToStringConverter()));

        coerced.Write(null).NULL.Should().BeTrue();
        coerced.ToPartiQlLiteral(null).Should().Be("NULL");

        var written = coerced.Write(new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"));
        written.S.Should().Be("0f8fad5b-d9cb-469f-a165-70867728950e");
        coerced
            .ReadObject(written, "Path", false, null)
            .Should()
            .Be(new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"));
        coerced
            .ReadObject(new AttributeValue { NULL = true }, "Path", false, null)
            .Should()
            .BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CoerceReaderWriter_AotConvertedEnumWrapper_RoundTripsEnumElement()
    {
        var coerced = DynamoValueReaderWriterFactory.CoerceReaderWriter<ElementStatus>(
            CreateAotWrapper(new EnumToStringConverter<ElementStatus>()));

        var attributeValue = coerced.Write(ElementStatus.Active);

        attributeValue.S.Should().Be("Active");
        coerced
            .ReadObject(new AttributeValue { S = "Active" }, "Path", true, null)
            .Should()
            .Be(ElementStatus.Active);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Create_BuildsListCodecFromAotConvertedElementWrapper()
    {
        var elementReaderWriter = CreateAotWrapper(new GuidToStringConverter());

        var listReaderWriter =
            DynamoValueReaderWriterFactory.Create(typeof(List<Guid>), elementReaderWriter)!;

        var written = listReaderWriter.WriteBoxed(
            new List<Guid> { new("0f8fad5b-d9cb-469f-a165-70867728950e") });
        written.L.Should().HaveCount(1);
        written.L[0].S.Should().Be("0f8fad5b-d9cb-469f-a165-70867728950e");

        var readBack = listReaderWriter.ReadObject(written, "Path", true, null);
        readBack
            .Should()
            .BeEquivalentTo(new List<Guid> { new("0f8fad5b-d9cb-469f-a165-70867728950e") });
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Create_BuildsSetAndDictionaryCodecsFromAotConvertedElementWrapper()
    {
        var elementReaderWriter = CreateAotWrapper(new EnumToStringConverter<ElementStatus>());

        var setReaderWriter = DynamoValueReaderWriterFactory.Create(
            typeof(HashSet<ElementStatus>),
            elementReaderWriter)!;
        var writtenSet = setReaderWriter.WriteBoxed(
            new HashSet<ElementStatus> { ElementStatus.Active });
        writtenSet.SS.Should().Equal("Active");

        var dictionaryReaderWriter = DynamoValueReaderWriterFactory.Create(
            typeof(Dictionary<string, ElementStatus>),
            elementReaderWriter)!;
        var writtenDictionary = dictionaryReaderWriter.WriteBoxed(
            new Dictionary<string, ElementStatus> { ["first"] = ElementStatus.Active });
        writtenDictionary.M["first"].S.Should().Be("Active");
        dictionaryReaderWriter
            .ReadObject(writtenDictionary, "Path", true, null)
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, ElementStatus> { ["first"] = ElementStatus.Active });
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CoerceReaderWriter_TypedWrapperStillAcceptedDirectly()
    {
        // JIT composes typed wrappers; coercion must keep returning them untouched.
        var converted = DynamoValueReaderWriterFactory.Compose(
            new GuidToStringConverter(),
            new StringDynamoValueReaderWriter());

        converted.Should().BeAssignableTo<DynamoValueReaderWriter<Guid>>();
        DynamoValueReaderWriterFactory
            .CoerceReaderWriter<Guid>(converted!)
            .Should()
            .BeSameAs(converted);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CoerceReaderWriter_UnconvertibleTypeStillThrows()
    {
        var act = () => DynamoValueReaderWriterFactory.CoerceReaderWriter<DateTime>(
            CreateAotWrapper(new GuidToStringConverter()));

        act.Should().Throw<InvalidCastException>();
    }

    private static DynamoAotConvertedValueReaderWriter CreateAotWrapper(ValueConverter converter)
        => new(new StringDynamoValueReaderWriter(), converter);

    private enum ElementStatus
    {
        Active
    }
}
