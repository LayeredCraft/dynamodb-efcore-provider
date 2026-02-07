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
        var dbContextOptions = new DbContextOptionsBuilder<DbContext>().Options;

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
                            Items = new List<Dictionary<string, AttributeValue>>
                            {
                                new() { ["id"] = new AttributeValue { S = "A" } },
                                new() { ["id"] = new AttributeValue { S = "B" } },
                            },
                            NextToken = "t1",
                        });

                if (request.NextToken == "t1")
                    return Task.FromResult(
                        new ExecuteStatementResponse
                        {
                            Items = new List<Dictionary<string, AttributeValue>>
                            {
                                new() { ["id"] = new AttributeValue { S = "C" } },
                            },
                            NextToken = null,
                        });

                return Task.FromResult(
                    new ExecuteStatementResponse
                    {
                        Items = new List<Dictionary<string, AttributeValue>>(), NextToken = null,
                    });
            });

        var wrapper = new TestDynamoClientWrapper(
            dbContextOptions,
            executionStrategy,
            diagnosticsLogger,
            client);

        var requestPrototype = new ExecuteStatementRequest
        {
            Statement = "SELECT * FROM Test", Parameters = new List<AttributeValue>(),
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
