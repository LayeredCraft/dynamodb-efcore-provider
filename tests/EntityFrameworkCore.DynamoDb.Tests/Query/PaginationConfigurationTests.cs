using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Tests for DynamoDbOptionsExtension configuration (excluding removed DefaultPageSize).</summary>
public class PaginationConfigurationTests
{
    [Fact]
    public void DynamoDbOptionsExtension_DefaultValues_AreCorrect()
    {
        var extension = new DynamoDbOptionsExtension();

        extension.AutomaticIndexSelectionMode.Should().Be(DynamoAutomaticIndexSelectionMode.Off);
        extension.DynamoDbClient.Should().BeNull();
        extension.DynamoDbClientConfig.Should().BeNull();
        extension.DynamoDbClientConfigAction.Should().BeNull();
        extension.TransactionOverflowBehavior.Should().Be(TransactionOverflowBehavior.Throw);
        extension.MaxTransactionSize.Should().Be(100);
        extension.MaxBatchWriteSize.Should().Be(25);
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
    public void Clone_PreservesAllProperties()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var config = new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" };
        Action<AmazonDynamoDBConfig> callback = c => c.UseHttp = true;
        var original = new DynamoDbOptionsExtension()
            .WithDynamoDbClient(client)
            .WithDynamoDbClientConfig(config)
            .WithDynamoDbClientConfigAction(callback)
            .WithAutomaticIndexSelectionMode(DynamoAutomaticIndexSelectionMode.Conservative)
            .WithTransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking)
            .WithMaxTransactionSize(42)
            .WithMaxBatchWriteSize(11);

        // Clone is protected; trigger via a With method.
        var cloned =
            original.WithAutomaticIndexSelectionMode(DynamoAutomaticIndexSelectionMode.SuggestOnly);

        cloned.DynamoDbClient.Should().BeSameAs(client);
        cloned.DynamoDbClientConfig.Should().BeSameAs(config);
        cloned.DynamoDbClientConfigAction.Should().BeSameAs(callback);
        cloned
            .AutomaticIndexSelectionMode
            .Should()
            .Be(DynamoAutomaticIndexSelectionMode.SuggestOnly);
        cloned.TransactionOverflowBehavior.Should().Be(TransactionOverflowBehavior.UseChunking);
        cloned.MaxTransactionSize.Should().Be(42);
        cloned.MaxBatchWriteSize.Should().Be(11);
    }

    [Fact]
    public void WithAutomaticIndexSelectionMode_SetsMode()
    {
        var extension = new DynamoDbOptionsExtension();

        var updated =
            extension.WithAutomaticIndexSelectionMode(
                DynamoAutomaticIndexSelectionMode.SuggestOnly);

        updated
            .AutomaticIndexSelectionMode
            .Should()
            .Be(DynamoAutomaticIndexSelectionMode.SuggestOnly);
    }

    [Fact]
    public void UseDynamo_ConfigureAutomaticIndexSelection_StoresModeOnOptionsExtension()
    {
        var optionsBuilder = new DbContextOptionsBuilder();

        optionsBuilder.UseDynamo(options
            => options.UseAutomaticIndexSelection(DynamoAutomaticIndexSelectionMode.Conservative));

        var extension = optionsBuilder.Options.FindExtension<DynamoDbOptionsExtension>();

        extension.Should().NotBeNull();
        extension!
            .AutomaticIndexSelectionMode
            .Should()
            .Be(DynamoAutomaticIndexSelectionMode.Conservative);
    }

    [Fact]
    public void UseDynamo_ConfigureTransactionOverflowBehavior_StoresValueOnOptionsExtension()
    {
        var optionsBuilder = new DbContextOptionsBuilder();

        optionsBuilder.UseDynamo(options
            => options.TransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking));

        var extension = optionsBuilder.Options.FindExtension<DynamoDbOptionsExtension>();

        extension.Should().NotBeNull();
        extension!.TransactionOverflowBehavior.Should().Be(TransactionOverflowBehavior.UseChunking);
    }

    [Fact]
    public void UseDynamo_ConfigureMaxTransactionSize_StoresValueOnOptionsExtension()
    {
        var optionsBuilder = new DbContextOptionsBuilder();

        optionsBuilder.UseDynamo(options => options.MaxTransactionSize(12));

        var extension = optionsBuilder.Options.FindExtension<DynamoDbOptionsExtension>();

        extension.Should().NotBeNull();
        extension!.MaxTransactionSize.Should().Be(12);
    }

    [Fact]
    public void UseDynamo_ConfigureMaxBatchWriteSize_StoresValueOnOptionsExtension()
    {
        var optionsBuilder = new DbContextOptionsBuilder();

        optionsBuilder.UseDynamo(options => options.MaxBatchWriteSize(9));

        var extension = optionsBuilder.Options.FindExtension<DynamoDbOptionsExtension>();

        extension.Should().NotBeNull();
        extension!.MaxBatchWriteSize.Should().Be(9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void WithMaxTransactionSize_InvalidValue_Throws(int invalidValue)
    {
        var extension = new DynamoDbOptionsExtension();

        Action act = () => extension.WithMaxTransactionSize(invalidValue);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(26)]
    public void WithMaxBatchWriteSize_InvalidValue_Throws(int invalidValue)
    {
        var extension = new DynamoDbOptionsExtension();

        Action act = () => extension.WithMaxBatchWriteSize(invalidValue);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ServiceProviderHash_IncludesAutomaticIndexSelectionMode()
    {
        var extension1 =
            new DynamoDbOptionsExtension().WithAutomaticIndexSelectionMode(
                DynamoAutomaticIndexSelectionMode.Off);
        var extension2 =
            new DynamoDbOptionsExtension().WithAutomaticIndexSelectionMode(
                DynamoAutomaticIndexSelectionMode.Conservative);

        extension1
            .Info
            .GetServiceProviderHashCode()
            .Should()
            .NotBe(extension2.Info.GetServiceProviderHashCode());
    }

    [Fact]
    public void ServiceProviderHash_IncludesTransactionAndBatchSizingOptions()
    {
        var extension1 = new DynamoDbOptionsExtension()
            .WithTransactionOverflowBehavior(TransactionOverflowBehavior.Throw)
            .WithMaxTransactionSize(100)
            .WithMaxBatchWriteSize(25);
        var extension2 = new DynamoDbOptionsExtension()
            .WithTransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking)
            .WithMaxTransactionSize(64)
            .WithMaxBatchWriteSize(10);

        extension1
            .Info
            .GetServiceProviderHashCode()
            .Should()
            .NotBe(extension2.Info.GetServiceProviderHashCode());
    }

    [Fact]
    public void ShouldUseSameServiceProvider_SameSettings_ReturnsTrue()
    {
        var extension1 =
            new DynamoDbOptionsExtension().WithAutomaticIndexSelectionMode(
                DynamoAutomaticIndexSelectionMode.Conservative);
        var extension2 =
            new DynamoDbOptionsExtension().WithAutomaticIndexSelectionMode(
                DynamoAutomaticIndexSelectionMode.Conservative);

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
