using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.Storage;

public class DynamoClientWrapperTests
{
    [Fact]
    public async Task ExecutePartiQl_Reenumeration_UsesFreshContinuationToken()
    {
        var diagnosticsLogger =
            Substitute.For<IDiagnosticsLogger<DbLoggerCategory.Database.Command>>();
        diagnosticsLogger.Logger.Returns(NullLogger.Instance);

        var executionStrategy = new TestExecutionStrategy();
        var dbContextOptions = new DbContextOptionsBuilder<DbContext>().UseDynamo().Options;

        var client = Substitute.For<IAmazonDynamoDB>();
        var nextTokens = new List<string?>();

        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(req => nextTokens.Add(req.NextToken)),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<ExecuteStatementRequest>();
                if (string.IsNullOrEmpty(request.NextToken))
                    return Task.FromResult(
                        new ExecuteStatementResponse
                        {
                            Items =
                            [
                                new() { ["id"] = new AttributeValue { S = "A" } },
                                new() { ["id"] = new AttributeValue { S = "B" } },
                            ],
                            NextToken = "t1",
                        });

                if (request.NextToken == "t1")
                    return Task.FromResult(
                        new ExecuteStatementResponse
                        {
                            Items =
                            [
                                new Dictionary<string, AttributeValue>
                                {
                                    ["id"] = new() { S = "C" },
                                },
                            ],
                            NextToken = null,
                        });

                return Task.FromResult(
                    new ExecuteStatementResponse { Items = [], NextToken = null });
            });

        var wrapper = new TestDynamoClientWrapper(
            dbContextOptions,
            executionStrategy,
            diagnosticsLogger,
            client);

        var requestPrototype = new ExecuteStatementRequest
        {
            Statement = "SELECT * FROM Test", Parameters = [],
        };

        var enumerable = wrapper.ExecutePartiQl(requestPrototype, false);

        var firstResults = await EnumerateAsync(enumerable);
        var secondResults = await EnumerateAsync(enumerable);

        firstResults.Should().HaveCount(3);
        secondResults.Should().HaveCount(3);
        requestPrototype.NextToken.Should().BeNull();

        nextTokens.Should().HaveCount(4);
        nextTokens[0].Should().BeNull();
        nextTokens[1].Should().Be("t1");
        nextTokens[2].Should().BeNull();
        nextTokens[3].Should().Be("t1");
    }

    [Fact]
    public void Client_WhenConfiguredClientProvided_UsesConfiguredClient()
    {
        var diagnosticsLogger =
            Substitute.For<IDiagnosticsLogger<DbLoggerCategory.Database.Command>>();
        diagnosticsLogger.Logger.Returns(NullLogger.Instance);

        var executionStrategy = new TestExecutionStrategy();
        var configuredClient = Substitute.For<IAmazonDynamoDB>();
        var dbContextOptions = new DbContextOptionsBuilder<DbContext>()
            .UseDynamo(options => options.DynamoDbClient(configuredClient))
            .Options;

        var wrapper =
            new DynamoClientWrapper(dbContextOptions, executionStrategy, diagnosticsLogger);

        wrapper.Client.Should().BeSameAs(configuredClient);
    }

    [Fact]
    public void Client_WhenOnlyConfigProvided_UsesConfiguredServiceUrl()
    {
        var diagnosticsLogger =
            Substitute.For<IDiagnosticsLogger<DbLoggerCategory.Database.Command>>();
        diagnosticsLogger.Logger.Returns(NullLogger.Instance);

        var executionStrategy = new TestExecutionStrategy();
        var dbContextOptions = new DbContextOptionsBuilder<DbContext>().UseDynamo(options
                => options.DynamoDbClientConfig(
                    new AmazonDynamoDBConfig { ServiceURL = "http://localhost:7001" }))
            .Options;

        var wrapper =
            new DynamoClientWrapper(dbContextOptions, executionStrategy, diagnosticsLogger);

        wrapper.Client.Config.ServiceURL.Should().StartWith("http://localhost:7001");
    }

    [Fact]
    public void Client_WhenConfigAndBuilderOverridesProvided_UsesBuilderOverridesLast()
    {
        var diagnosticsLogger =
            Substitute.For<IDiagnosticsLogger<DbLoggerCategory.Database.Command>>();
        diagnosticsLogger.Logger.Returns(NullLogger.Instance);

        var executionStrategy = new TestExecutionStrategy();
        var dbContextOptions = new DbContextOptionsBuilder<DbContext>().UseDynamo(options =>
            {
                options.DynamoDbClientConfig(
                    new AmazonDynamoDBConfig
                    {
                        ServiceURL = "http://localhost:7001", AuthenticationRegion = "us-west-1",
                    });
                options.ServiceUrl("http://localhost:8000");
                options.AuthenticationRegion("us-east-1");
            })
            .Options;

        var wrapper =
            new DynamoClientWrapper(dbContextOptions, executionStrategy, diagnosticsLogger);

        wrapper.Client.Config.ServiceURL.Should().StartWith("http://localhost:8000");
        wrapper.Client.Config.AuthenticationRegion.Should().Be("us-east-1");
    }

    [Fact]
    public void Client_WhenConfigCallbackProvided_InvokesCallbackBeforeBuilderOverrides()
    {
        var diagnosticsLogger =
            Substitute.For<IDiagnosticsLogger<DbLoggerCategory.Database.Command>>();
        diagnosticsLogger.Logger.Returns(NullLogger.Instance);

        var executionStrategy = new TestExecutionStrategy();
        var dbContextOptions = new DbContextOptionsBuilder<DbContext>().UseDynamo(options =>
            {
                options.ConfigureDynamoDbClientConfig(config =>
                {
                    config.ServiceURL = "http://localhost:7001";
                    config.AuthenticationRegion = "us-west-2";
                    config.UseHttp = true;
                });
                options.ServiceUrl("http://localhost:8000");
            })
            .Options;

        var wrapper =
            new DynamoClientWrapper(dbContextOptions, executionStrategy, diagnosticsLogger);

        wrapper.Client.Config.ServiceURL.Should().StartWith("http://localhost:8000");
        wrapper.Client.Config.AuthenticationRegion.Should().Be("us-west-2");
        wrapper.Client.Config.UseHttp.Should().BeTrue();
    }

    private static async Task<List<Dictionary<string, AttributeValue>>> EnumerateAsync(
        IAsyncEnumerable<Dictionary<string, AttributeValue>> enumerable)
    {
        var results = new List<Dictionary<string, AttributeValue>>();
        await foreach (var item in enumerable)
            results.Add(item);
        return results;
    }

    private sealed class TestExecutionStrategy : IExecutionStrategy
    {
        public bool RetriesOnFailure => false;

        public TResult Execute<TState, TResult>(
            TState state,
            Func<DbContext, TState, TResult> operation,
            Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded)
            => operation(null!, state);

        public Task<TResult> ExecuteAsync<TState, TResult>(
            TState state,
            Func<DbContext, TState, CancellationToken, Task<TResult>> operation,
            Func<DbContext, TState, CancellationToken, Task<ExecutionResult<TResult>>>?
                verifySucceeded,
            CancellationToken cancellationToken = default)
            => operation(null!, state, cancellationToken);
    }

    private sealed class TestDynamoClientWrapper(
        IDbContextOptions dbContextOptions,
        IExecutionStrategy executionStrategy,
        IDiagnosticsLogger<DbLoggerCategory.Database.Command> commandLogger,
        IAmazonDynamoDB client) : DynamoClientWrapper(
        dbContextOptions,
        executionStrategy,
        commandLogger)
    {
        public override IAmazonDynamoDB Client { get; } = client;
    }
}
