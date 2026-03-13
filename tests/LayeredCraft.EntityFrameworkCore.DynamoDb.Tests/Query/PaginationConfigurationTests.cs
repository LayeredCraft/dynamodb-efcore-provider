using Amazon.DynamoDBv2;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Represents the PaginationConfigurationTests type.</summary>
public class PaginationConfigurationTests
{
    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void DynamoDbOptionsExtension_DefaultValues_AreCorrect()
    {
        var extension = new DynamoDbOptionsExtension();

        extension.DefaultPageSize.Should().BeNull();
        extension.AutomaticIndexSelectionMode.Should().Be(DynamoAutomaticIndexSelectionMode.Off);
        extension.DynamoDbClient.Should().BeNull();
        extension.DynamoDbClientConfig.Should().BeNull();
        extension.DynamoDbClientConfigAction.Should().BeNull();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void WithDynamoDbClient_SetsClient()
    {
        var extension = new DynamoDbOptionsExtension();
        var client = Substitute.For<IAmazonDynamoDB>();

        var updated = extension.WithDynamoDbClient(client);

        updated.DynamoDbClient.Should().BeSameAs(client);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void WithDynamoDbClientConfig_SetsConfig()
    {
        var extension = new DynamoDbOptionsExtension();
        var config = new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" };

        var updated = extension.WithDynamoDbClientConfig(config);

        updated.DynamoDbClientConfig.Should().BeSameAs(config);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void WithDynamoDbClientConfigAction_SetsCallback()
    {
        var extension = new DynamoDbOptionsExtension();
        Action<AmazonDynamoDBConfig> callback = config
            => config.ServiceURL = "http://localhost:8000";

        var updated = extension.WithDynamoDbClientConfigAction(callback);

        updated.DynamoDbClientConfigAction.Should().BeSameAs(callback);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void WithDefaultPageSize_SetsPageSize()
    {
        var extension = new DynamoDbOptionsExtension();

        var updated = extension.WithDefaultPageSize(100);

        updated.DefaultPageSize.Should().Be(100);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void WithDefaultPageSize_Zero_ThrowsException()
    {
        var extension = new DynamoDbOptionsExtension();

        var act = () => extension.WithDefaultPageSize(0);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("pageSize");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void WithDefaultPageSize_Negative_ThrowsException()
    {
        var extension = new DynamoDbOptionsExtension();

        var act = () => extension.WithDefaultPageSize(-1);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("pageSize");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void Clone_PreservesAllProperties()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var config = new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" };
        Action<AmazonDynamoDBConfig> callback = c => c.UseHttp = true;
        var original = new DynamoDbOptionsExtension()
            .WithDynamoDbClient(client)
            .WithDynamoDbClientConfig(config)
            .WithDynamoDbClientConfigAction(callback)
            .WithDefaultPageSize(50)
            .WithAutomaticIndexSelectionMode(DynamoAutomaticIndexSelectionMode.Conservative);

        // Clone is protected, so we use one of the With methods which calls Clone
        var cloned = original.WithDefaultPageSize(75);

        cloned.DynamoDbClient.Should().BeSameAs(client);
        cloned.DynamoDbClientConfig.Should().BeSameAs(config);
        cloned.DynamoDbClientConfigAction.Should().BeSameAs(callback);
        cloned.DefaultPageSize.Should().Be(75); // Updated value
        cloned.AutomaticIndexSelectionMode.Should().Be(DynamoAutomaticIndexSelectionMode.Conservative);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ServiceProviderHash_IncludesDefaultPageSize()
    {
        var extension1 = new DynamoDbOptionsExtension().WithDefaultPageSize(100);

        var extension2 = new DynamoDbOptionsExtension().WithDefaultPageSize(200);

        var hash1 = extension1.Info.GetServiceProviderHashCode();
        var hash2 = extension2.Info.GetServiceProviderHashCode();

        hash1.Should().NotBe(hash2);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ShouldUseSameServiceProvider_DifferentDefaultPageSize_ReturnsFalse()
    {
        var extension1 = new DynamoDbOptionsExtension().WithDefaultPageSize(50);

        var extension2 = new DynamoDbOptionsExtension().WithDefaultPageSize(100);

        extension1.Info.ShouldUseSameServiceProvider(extension2.Info).Should().BeFalse();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void WithAutomaticIndexSelectionMode_SetsMode()
    {
        var extension = new DynamoDbOptionsExtension();

        var updated = extension.WithAutomaticIndexSelectionMode(DynamoAutomaticIndexSelectionMode.SuggestOnly);

        updated.AutomaticIndexSelectionMode.Should().Be(DynamoAutomaticIndexSelectionMode.SuggestOnly);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void UseDynamo_ConfigureAutomaticIndexSelection_StoresModeOnOptionsExtension()
    {
        var optionsBuilder = new DbContextOptionsBuilder();

        optionsBuilder.UseDynamo(options =>
            options.UseAutomaticIndexSelection(DynamoAutomaticIndexSelectionMode.Conservative));

        var extension = optionsBuilder.Options.FindExtension<DynamoDbOptionsExtension>();

        extension.Should().NotBeNull();
        extension!.AutomaticIndexSelectionMode.Should().Be(DynamoAutomaticIndexSelectionMode.Conservative);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ServiceProviderHash_IncludesAutomaticIndexSelectionMode()
    {
        var extension1 = new DynamoDbOptionsExtension()
            .WithAutomaticIndexSelectionMode(DynamoAutomaticIndexSelectionMode.Off);

        var extension2 = new DynamoDbOptionsExtension()
            .WithAutomaticIndexSelectionMode(DynamoAutomaticIndexSelectionMode.Conservative);

        var hash1 = extension1.Info.GetServiceProviderHashCode();
        var hash2 = extension2.Info.GetServiceProviderHashCode();

        hash1.Should().NotBe(hash2);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ShouldUseSameServiceProvider_SameSettings_ReturnsTrue()
    {
        var extension1 = new DynamoDbOptionsExtension().WithDefaultPageSize(100);

        var extension2 = new DynamoDbOptionsExtension().WithDefaultPageSize(100);

        extension1.Info.ShouldUseSameServiceProvider(extension2.Info).Should().BeTrue();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ShouldUseSameServiceProvider_DifferentClient_ReturnsFalse()
    {
        var extension1 =
            new DynamoDbOptionsExtension().WithDynamoDbClient(Substitute.For<IAmazonDynamoDB>());
        var extension2 =
            new DynamoDbOptionsExtension().WithDynamoDbClient(Substitute.For<IAmazonDynamoDB>());

        extension1.Info.ShouldUseSameServiceProvider(extension2.Info).Should().BeFalse();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ShouldUseSameServiceProvider_DifferentConfig_ReturnsFalse()
    {
        var extension1 = new DynamoDbOptionsExtension().WithDynamoDbClientConfig(
            new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" });
        var extension2 = new DynamoDbOptionsExtension().WithDynamoDbClientConfig(
            new AmazonDynamoDBConfig { ServiceURL = "http://localhost:9000" });

        extension1.Info.ShouldUseSameServiceProvider(extension2.Info).Should().BeFalse();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ShouldUseSameServiceProvider_DifferentConfigCallback_ReturnsFalse()
    {
        Action<AmazonDynamoDBConfig> callback1 = c => c.ServiceURL = "http://localhost:8000";
        Action<AmazonDynamoDBConfig> callback2 = c => c.ServiceURL = "http://localhost:9000";

        var extension1 = new DynamoDbOptionsExtension().WithDynamoDbClientConfigAction(callback1);
        var extension2 = new DynamoDbOptionsExtension().WithDynamoDbClientConfigAction(callback2);

        extension1.Info.ShouldUseSameServiceProvider(extension2.Info).Should().BeFalse();
    }
}
