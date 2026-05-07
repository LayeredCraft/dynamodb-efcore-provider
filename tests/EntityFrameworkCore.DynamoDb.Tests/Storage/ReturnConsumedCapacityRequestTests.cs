using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

/// <summary>Tests provider-level consumed capacity request configuration.</summary>
public class ReturnConsumedCapacityRequestTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecutePartiQl_AppliesConfiguredReturnConsumedCapacity()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ExecuteStatementRequest? captured = null;
        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [] });
        await using var context = RequestContext.Create(client, ReturnConsumedCapacity.INDEXES);
        var wrapper = context.GetService<IDynamoClientWrapper>();

        await foreach (var _ in wrapper.ExecutePartiQl(
            new ExecuteStatementRequest { Statement = "SELECT * FROM T" })) { }

        captured.Should().NotBeNull();
        captured!.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.INDEXES);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecutePartiQl_DoesNotMutateCallerRequest()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ExecuteStatementRequest? captured = null;
        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [] });
        await using var context = RequestContext.Create(client, ReturnConsumedCapacity.INDEXES);
        var request = new ExecuteStatementRequest { Statement = "SELECT * FROM T" };

        await foreach (var _ in
            context.GetService<IDynamoClientWrapper>().ExecutePartiQl(request)) { }

        request.ReturnConsumedCapacity.Should().BeNull();
        captured.Should().NotBeNull();
        captured.Should().NotBeSameAs(request);
        captured!.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.INDEXES);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecutePartiQl_PreservesExplicitReturnConsumedCapacity()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ExecuteStatementRequest? captured = null;
        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [] });
        await using var context = RequestContext.Create(client, ReturnConsumedCapacity.INDEXES);

        await foreach (var _ in context
            .GetService<IDynamoClientWrapper>()
            .ExecutePartiQl(
                new ExecuteStatementRequest
                {
                    Statement = "SELECT * FROM T",
                    ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL,
                })) { }

        captured.Should().NotBeNull();
        captured!.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecutePartiQl_PreservesSeedNextToken()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ExecuteStatementRequest? captured = null;
        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [] });
        await using var context = RequestContext.Create(client, ReturnConsumedCapacity.INDEXES);

        await foreach (var _ in context
            .GetService<IDynamoClientWrapper>()
            .ExecutePartiQl(
                new ExecuteStatementRequest
                {
                    Statement = "SELECT * FROM T", NextToken = "seed-token",
                })) { }

        captured.Should().NotBeNull();
        captured!.NextToken.Should().Be("seed-token");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteWriteAsync_AppliesConfiguredReturnConsumedCapacity()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ExecuteStatementRequest? captured = null;
        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse());
        await using var context = RequestContext.Create(client, ReturnConsumedCapacity.TOTAL);

        await context.GetService<IDynamoClientWrapper>().ExecuteWriteAsync("DELETE FROM T", []);

        captured.Should().NotBeNull();
        captured!.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteBatchWriteAsync_AppliesConfiguredReturnConsumedCapacity()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BatchExecuteStatementRequest? captured = null;
        client
            .BatchExecuteStatementAsync(
                Arg.Do<BatchExecuteStatementRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
            .Returns(new BatchExecuteStatementResponse { Responses = [] });
        await using var context = RequestContext.Create(client, ReturnConsumedCapacity.TOTAL);

        await context
            .GetService<IDynamoClientWrapper>()
            .ExecuteBatchWriteAsync([new BatchStatementRequest { Statement = "DELETE FROM T" }]);

        captured.Should().NotBeNull();
        captured!.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteTransactionAsync_AppliesConfiguredReturnConsumedCapacity()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ExecuteTransactionRequest? captured = null;
        client
            .ExecuteTransactionAsync(
                Arg.Do<ExecuteTransactionRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
            .Returns(new ExecuteTransactionResponse());
        await using var context = RequestContext.Create(client, ReturnConsumedCapacity.TOTAL);

        await context
            .GetService<IDynamoClientWrapper>()
            .ExecuteTransactionAsync([new ParameterizedStatement { Statement = "DELETE FROM T" }]);

        captured.Should().NotBeNull();
        captured!.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    private sealed class RequestContext(DbContextOptions<RequestContext> options) : DbContext(
        options)
    {
        public static RequestContext Create(
            IAmazonDynamoDB client,
            ReturnConsumedCapacity? returnConsumedCapacity = null)
        {
            var optionsBuilder = new DbContextOptionsBuilder<RequestContext>();
            optionsBuilder
                .UseDynamo(options => options
                    .DynamoDbClient(client)
                    .ReturnConsumedCapacity(returnConsumedCapacity))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

            return new RequestContext(optionsBuilder.Options);
        }
    }
}
