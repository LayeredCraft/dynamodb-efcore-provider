using System.Globalization;
using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

public class DynamoDbOptionsExtensionInfoTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void DefaultOptions_ProduceExactLogFragmentAndDebugInfo()
    {
        var extension = new DynamoDbOptionsExtension();

        extension
            .Info
            .LogFragment
            .Should()
            .Be(
                "AutomaticIndexSelectionMode=On TransactionOverflowBehavior=Throw MaxTransactionSize=100 "
                + "MaxBatchWriteSize=25 ReturnConsumedCapacity=null ConsistentRead=False "
                + "AllowUnsafeFilteredQueries=False TableLifecycleWaitForCompletion=True "
                + "TableLifecycleInitialPollingDelay=00:00:01 TableLifecycleMaxPollingDelay=00:00:05 "
                + "TableLifecycleBackoffMultiplier=1.5 TableLifecycleTimeout=00:10:00 "
                + "DynamoDbClient=False DynamoDbClientConfig=False DynamoDbClientConfigAction=False ");

        GetDebugInfo(extension)
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["DynamoDB:AutomaticIndexSelectionMode"] = "On",
                    ["DynamoDB:TransactionOverflowBehavior"] = "Throw",
                    ["DynamoDB:MaxTransactionSize"] = "100",
                    ["DynamoDB:MaxBatchWriteSize"] = "25",
                    ["DynamoDB:ReturnConsumedCapacity"] = "null",
                    ["DynamoDB:ConsistentRead"] = "False",
                    ["DynamoDB:AllowUnsafeFilteredQueries"] = "False",
                    ["DynamoDB:TableLifecycleWaitForCompletion"] = "True",
                    ["DynamoDB:TableLifecycleInitialPollingDelay"] = "00:00:01",
                    ["DynamoDB:TableLifecycleMaxPollingDelay"] = "00:00:05",
                    ["DynamoDB:TableLifecycleBackoffMultiplier"] = "1.5",
                    ["DynamoDB:TableLifecycleTimeout"] = "00:10:00",
                    ["DynamoDB:DynamoDbClient"] = "0",
                    ["DynamoDB:DynamoDbClientConfig"] = "0",
                    ["DynamoDB:DynamoDbClientConfigAction"] = "0"
                });
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ConfiguredOptions_ProduceSafeLogFragmentAndDebugInfo()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var config = new AmazonDynamoDBConfig
        {
            AuthenticationRegion = "us-east-1",
            ServiceURL = "https://user:secret@example.test:8443/path?query=value#fragment"
        };
        Action<AmazonDynamoDBConfig> configure = _ => throw new InvalidOperationException();
        var extension = new DynamoDbOptionsExtension()
            .WithDynamoDbClient(client)
            .WithDynamoDbClientConfig(config)
            .WithDynamoDbClientConfigAction(configure)
            .WithAutomaticIndexSelectionMode(DynamoAutomaticIndexSelectionMode.SuggestOnly)
            .WithTransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking)
            .WithMaxTransactionSize(42)
            .WithMaxBatchWriteSize(11)
            .WithReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .WithConsistentRead(true)
            .WithAllowUnsafeFilteredQueries(true)
            .WithTableLifecycleOptions(options =>
            {
                options.WaitForCompletion = false;
                options.InitialPollingDelay = TimeSpan.FromMilliseconds(10);
                options.MaxPollingDelay = TimeSpan.FromMilliseconds(20);
                options.BackoffMultiplier = 2;
                options.Timeout = TimeSpan.FromSeconds(1);
            });

        var logFragment = extension.Info.LogFragment;
        var debugInfo = GetDebugInfo(extension);

        logFragment
            .Should()
            .Be(
                "AutomaticIndexSelectionMode=SuggestOnly TransactionOverflowBehavior=UseChunking "
                + "MaxTransactionSize=42 MaxBatchWriteSize=11 ReturnConsumedCapacity=TOTAL "
                + "ConsistentRead=True AllowUnsafeFilteredQueries=True TableLifecycleWaitForCompletion=False "
                + "TableLifecycleInitialPollingDelay=00:00:00.0100000 "
                + "TableLifecycleMaxPollingDelay=00:00:00.0200000 TableLifecycleBackoffMultiplier=2 "
                + "TableLifecycleTimeout=00:00:01 DynamoDbClient=True DynamoDbClientConfig=True "
                + "DynamoDbClientConfigAction=True AuthenticationRegion=us-east-1 "
                + "ServiceURL=https://example.test:8443 ");
        debugInfo
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["DynamoDB:AutomaticIndexSelectionMode"] = "SuggestOnly",
                    ["DynamoDB:TransactionOverflowBehavior"] = "UseChunking",
                    ["DynamoDB:MaxTransactionSize"] = "42",
                    ["DynamoDB:MaxBatchWriteSize"] = "11",
                    ["DynamoDB:ReturnConsumedCapacity"] = "TOTAL",
                    ["DynamoDB:ConsistentRead"] = "True",
                    ["DynamoDB:AllowUnsafeFilteredQueries"] = "True",
                    ["DynamoDB:TableLifecycleWaitForCompletion"] = "False",
                    ["DynamoDB:TableLifecycleInitialPollingDelay"] = "00:00:00.0100000",
                    ["DynamoDB:TableLifecycleMaxPollingDelay"] = "00:00:00.0200000",
                    ["DynamoDB:TableLifecycleBackoffMultiplier"] = "2",
                    ["DynamoDB:TableLifecycleTimeout"] = "00:00:01",
                    ["DynamoDB:DynamoDbClient"] =
                        client.GetHashCode().ToString(CultureInfo.InvariantCulture),
                    ["DynamoDB:DynamoDbClientConfig"] =
                        config.GetHashCode().ToString(CultureInfo.InvariantCulture),
                    ["DynamoDB:DynamoDbClientConfigAction"] =
                        configure.GetHashCode().ToString(CultureInfo.InvariantCulture),
                    ["DynamoDB:AuthenticationRegion"] = "us-east-1",
                    ["DynamoDB:ServiceURL"] = "https://example.test:8443"
                });

        AssertSensitiveValuesAreAbsent(logFragment);
        AssertSensitiveValuesAreAbsent(string.Join(' ', debugInfo.Values));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ExplicitClient_ReportsOnlyItsIdentity()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var extension = new DynamoDbOptionsExtension().WithDynamoDbClient(client);

        extension
            .Info
            .LogFragment
            .Should()
            .Contain("DynamoDbClient=True DynamoDbClientConfig=False ");

        var debugInfo = GetDebugInfo(extension);
        debugInfo["DynamoDB:DynamoDbClient"]
            .Should()
            .Be(client.GetHashCode().ToString(CultureInfo.InvariantCulture));
        debugInfo["DynamoDB:DynamoDbClientConfig"].Should().Be("0");
        debugInfo["DynamoDB:DynamoDbClientConfigAction"].Should().Be("0");
        debugInfo.Should().NotContainKey("DynamoDB:AuthenticationRegion");
        debugInfo.Should().NotContainKey("DynamoDB:ServiceURL");
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData("http://localhost", "http://localhost:80")]
    [InlineData("https://localhost", "https://localhost:443")]
    [InlineData("http://localhost:8000/path", "http://localhost:8000")]
    [InlineData("http://[::1]:8000/path", "http://[::1]:8000")]
    public void ServiceUrl_IsReducedToSchemeHostAndPort(string serviceUrl, string expected)
    {
        var extension = new DynamoDbOptionsExtension().WithDynamoDbClientConfig(
            new AmazonDynamoDBConfig { ServiceURL = serviceUrl });

        extension.Info.LogFragment.Should().Contain($"ServiceURL={expected} ");
        GetDebugInfo(extension)["DynamoDB:ServiceURL"].Should().Be(expected);
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData("file:///tmp/dynamodb")]
    public void HostlessServiceUrl_IsOmitted(string serviceUrl)
    {
        var extension = new DynamoDbOptionsExtension().WithDynamoDbClientConfig(
            new AmazonDynamoDBConfig { ServiceURL = serviceUrl });

        extension.Info.LogFragment.Should().NotContain("ServiceURL=");
        GetDebugInfo(extension).Should().NotContainKey("DynamoDB:ServiceURL");
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData("not a URL")]
    [InlineData("custom://example.test")]
    public void MalformedOrPortlessServiceUrl_IsRejected(string serviceUrl)
        => DynamoDbOptionsExtension
            .DynamoOptionsExtensionInfo
            .SanitizeServiceUrl(serviceUrl)
            .Should()
            .BeNull();

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ConfigAction_IsNeverInvokedForDiagnostics()
    {
        var invocations = 0;
        Action<AmazonDynamoDBConfig> configure = _ =>
        {
            invocations++;
            throw new InvalidOperationException(
                "This action must not run while producing diagnostics.");
        };
        var extension = new DynamoDbOptionsExtension().WithDynamoDbClientConfigAction(configure);

        _ = extension.Info.LogFragment;
        _ = GetDebugInfo(extension);

        invocations.Should().Be(0);
    }

    private static Dictionary<string, string> GetDebugInfo(DynamoDbOptionsExtension extension)
    {
        var debugInfo = new Dictionary<string, string>();
        extension.Info.PopulateDebugInfo(debugInfo);
        return debugInfo;
    }

    private static void AssertSensitiveValuesAreAbsent(string diagnostics)
    {
        diagnostics.Should().NotContain("user");
        diagnostics.Should().NotContain("secret");
        diagnostics.Should().NotContain("/path");
        diagnostics.Should().NotContain("query=value");
        diagnostics.Should().NotContain("fragment");
    }
}
