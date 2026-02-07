using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

public class PaginationConfigurationTests
{
    [Fact]
    public void DynamoDbOptionsExtension_DefaultValues_AreCorrect()
    {
        var extension = new DynamoDbOptionsExtension();

        extension.PaginationMode.Should().Be(DynamoPaginationMode.Auto);
        extension.DefaultPageSize.Should().BeNull();
    }

    [Fact]
    public void WithPaginationMode_SetsMode()
    {
        var extension = new DynamoDbOptionsExtension();

        var updated = extension.WithPaginationMode(DynamoPaginationMode.Always);

        updated.PaginationMode.Should().Be(DynamoPaginationMode.Always);
    }

    [Fact]
    public void WithDefaultPageSize_SetsPageSize()
    {
        var extension = new DynamoDbOptionsExtension();

        var updated = extension.WithDefaultPageSize(100);

        updated.DefaultPageSize.Should().Be(100);
    }

    [Fact]
    public void WithDefaultPageSize_Zero_ThrowsException()
    {
        var extension = new DynamoDbOptionsExtension();

        var act = () => extension.WithDefaultPageSize(0);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("pageSize");
    }

    [Fact]
    public void WithDefaultPageSize_Negative_ThrowsException()
    {
        var extension = new DynamoDbOptionsExtension();

        var act = () => extension.WithDefaultPageSize(-1);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("pageSize");
    }

    [Fact]
    public void Clone_PreservesAllProperties()
    {
        var original = new DynamoDbOptionsExtension()
            .WithAuthenticationRegion("us-west-2")
            .WithServiceUrl("http://localhost:8000")
            .WithPaginationMode(DynamoPaginationMode.Never)
            .WithDefaultPageSize(50);

        // Clone is protected, so we use one of the With methods which calls Clone
        var cloned = original.WithDefaultPageSize(75);

        cloned.AuthenticationRegion.Should().Be("us-west-2");
        cloned.ServiceUrl.Should().Be("http://localhost:8000");
        cloned.PaginationMode.Should().Be(DynamoPaginationMode.Never);
        cloned.DefaultPageSize.Should().Be(75); // Updated value
    }

    [Fact]
    public void ServiceProviderHash_IncludesPaginationSettings()
    {
        var extension1 = new DynamoDbOptionsExtension()
            .WithPaginationMode(DynamoPaginationMode.Auto)
            .WithDefaultPageSize(100);

        var extension2 = new DynamoDbOptionsExtension()
            .WithPaginationMode(DynamoPaginationMode.Always)
            .WithDefaultPageSize(100);

        var hash1 = extension1.Info.GetServiceProviderHashCode();
        var hash2 = extension2.Info.GetServiceProviderHashCode();

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ShouldUseSameServiceProvider_DifferentPaginationMode_ReturnsFalse()
    {
        var extension1 =
            new DynamoDbOptionsExtension().WithPaginationMode(DynamoPaginationMode.Auto);

        var extension2 =
            new DynamoDbOptionsExtension().WithPaginationMode(DynamoPaginationMode.Always);

        extension1.Info.ShouldUseSameServiceProvider(extension2.Info).Should().BeFalse();
    }

    [Fact]
    public void ShouldUseSameServiceProvider_DifferentDefaultPageSize_ReturnsFalse()
    {
        var extension1 = new DynamoDbOptionsExtension().WithDefaultPageSize(50);

        var extension2 = new DynamoDbOptionsExtension().WithDefaultPageSize(100);

        extension1.Info.ShouldUseSameServiceProvider(extension2.Info).Should().BeFalse();
    }

    [Fact]
    public void ShouldUseSameServiceProvider_SameSettings_ReturnsTrue()
    {
        var extension1 = new DynamoDbOptionsExtension()
            .WithPaginationMode(DynamoPaginationMode.Auto)
            .WithDefaultPageSize(100);

        var extension2 = new DynamoDbOptionsExtension()
            .WithPaginationMode(DynamoPaginationMode.Auto)
            .WithDefaultPageSize(100);

        extension1.Info.ShouldUseSameServiceProvider(extension2.Info).Should().BeTrue();
    }
}
