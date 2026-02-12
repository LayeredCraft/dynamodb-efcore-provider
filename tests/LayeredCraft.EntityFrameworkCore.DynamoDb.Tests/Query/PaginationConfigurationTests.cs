using Amazon.DynamoDBv2;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using NSubstitute;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

public class PaginationConfigurationTests
{
    [Fact]
    public void DynamoDbOptionsExtension_DefaultValues_AreCorrect()
    {
        var extension = new DynamoDbOptionsExtension();

        extension.DefaultPageSize.Should().BeNull();
        extension.DynamoDbClient.Should().BeNull();
        extension.DynamoDbClientConfig.Should().BeNull();
        extension.DynamoDbClientConfigAction.Should().BeNull();
    }

    [Fact]
    public void WithDynamoDbClient_SetsClient()
    {
        var extension = new DynamoDbOptionsExtension();
        var client = Substitute.For<IAmazonDynamoDB>();

        var updated = extension.WithDynamoDbClient(client);

        updated.DynamoDbClient.Should().BeSameAs(client);
    }

    [Fact]
    public void WithDynamoDbClientConfig_SetsConfig()
    {
        var extension = new DynamoDbOptionsExtension();
        var config = new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" };

        var updated = extension.WithDynamoDbClientConfig(config);

        updated.DynamoDbClientConfig.Should().BeSameAs(config);
    }

    [Fact]
    public void WithDynamoDbClientConfigAction_SetsCallback()
    {
        var extension = new DynamoDbOptionsExtension();
        Action<AmazonDynamoDBConfig> callback = config
            => config.ServiceURL = "http://localhost:8000";

        var updated = extension.WithDynamoDbClientConfigAction(callback);

        updated.DynamoDbClientConfigAction.Should().BeSameAs(callback);
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
        var client = Substitute.For<IAmazonDynamoDB>();
        var config = new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" };
        Action<AmazonDynamoDBConfig> callback = c => c.UseHttp = true;
        var original = new DynamoDbOptionsExtension()
            .WithDynamoDbClient(client)
            .WithDynamoDbClientConfig(config)
            .WithDynamoDbClientConfigAction(callback)
            .WithDefaultPageSize(50);

        // Clone is protected, so we use one of the With methods which calls Clone
        var cloned = original.WithDefaultPageSize(75);

        cloned.DynamoDbClient.Should().BeSameAs(client);
        cloned.DynamoDbClientConfig.Should().BeSameAs(config);
        cloned.DynamoDbClientConfigAction.Should().BeSameAs(callback);
        cloned.DefaultPageSize.Should().Be(75); // Updated value
    }

    [Fact]
    public void ServiceProviderHash_IncludesDefaultPageSize()
    {
        var extension1 = new DynamoDbOptionsExtension().WithDefaultPageSize(100);

        var extension2 = new DynamoDbOptionsExtension().WithDefaultPageSize(200);

        var hash1 = extension1.Info.GetServiceProviderHashCode();
        var hash2 = extension2.Info.GetServiceProviderHashCode();

        hash1.Should().NotBe(hash2);
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
        var extension1 = new DynamoDbOptionsExtension().WithDefaultPageSize(100);

        var extension2 = new DynamoDbOptionsExtension().WithDefaultPageSize(100);

        extension1.Info.ShouldUseSameServiceProvider(extension2.Info).Should().BeTrue();
    }

    [Fact]
    public void ShouldUseSameServiceProvider_DifferentClient_ReturnsFalse()
    {
        var extension1 =
            new DynamoDbOptionsExtension().WithDynamoDbClient(Substitute.For<IAmazonDynamoDB>());
        var extension2 =
            new DynamoDbOptionsExtension().WithDynamoDbClient(Substitute.For<IAmazonDynamoDB>());

        extension1.Info.ShouldUseSameServiceProvider(extension2.Info).Should().BeFalse();
    }

    [Fact]
    public void ShouldUseSameServiceProvider_DifferentConfig_ReturnsFalse()
    {
        var extension1 = new DynamoDbOptionsExtension().WithDynamoDbClientConfig(
            new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" });
        var extension2 = new DynamoDbOptionsExtension().WithDynamoDbClientConfig(
            new AmazonDynamoDBConfig { ServiceURL = "http://localhost:9000" });

        extension1.Info.ShouldUseSameServiceProvider(extension2.Info).Should().BeFalse();
    }

    [Fact]
    public void ShouldUseSameServiceProvider_DifferentConfigCallback_ReturnsFalse()
    {
        Action<AmazonDynamoDBConfig> callback1 = c => c.ServiceURL = "http://localhost:8000";
        Action<AmazonDynamoDBConfig> callback2 = c => c.ServiceURL = "http://localhost:9000";

        var extension1 = new DynamoDbOptionsExtension().WithDynamoDbClientConfigAction(callback1);
        var extension2 = new DynamoDbOptionsExtension().WithDynamoDbClientConfigAction(callback2);

        extension1.Info.ShouldUseSameServiceProvider(extension2.Info).Should().BeFalse();
    }
}
