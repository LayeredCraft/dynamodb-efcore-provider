using EntityFrameworkCore.DynamoDb.Storage.Internal;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

public class DynamoValueReaderWriterFactoryTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Create_CollectionShape_WithoutReflectionFallback_ThrowsWithCompiledModelGuidance()
    {
        var act = () => DynamoValueReaderWriterFactory.Create(
            typeof(List<string>),
            new StringDynamoValueReaderWriter(),
            readOnlyDictionary: false,
            allowReflectionFallback: false);

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*List<string>*compiled model*NativeAOT*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Create_CollectionShape_WithReflectionFallback_BuildsCodec()
    {
        var readerWriter = DynamoValueReaderWriterFactory.Create(
            typeof(List<string>),
            new StringDynamoValueReaderWriter(),
            readOnlyDictionary: false,
            allowReflectionFallback: true);

        readerWriter.Should().NotBeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Create_ScalarShape_WithoutReflectionFallback_StillBuildsCodec()
        => DynamoValueReaderWriterFactory
            .Create(typeof(string), allowReflectionFallback: false)
            .Should()
            .NotBeNull();
}
