using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

public class DynamoDbCommandInterceptionTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Intercepts_AllSupportedSdkCommands()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [] });
        client
            .ExecuteTransactionAsync(
                Arg.Any<ExecuteTransactionRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new ExecuteTransactionResponse());
        client
            .BatchExecuteStatementAsync(
                Arg.Any<BatchExecuteStatementRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new BatchExecuteStatementResponse { Responses = [] });
        var interceptor = new RecordingInterceptor();
        await using var context = InterceptionContext.Create(client, interceptor);
        var wrapper = context.GetService<IDynamoClientWrapper>();

        await foreach (var _ in wrapper.ExecutePartiQl(
            new ExecuteStatementRequest { Statement = "SELECT * FROM T" })) { }

        await wrapper.ExecuteWriteAsync("DELETE FROM T", []);
        await wrapper.ExecuteTransactionAsync(
            [new ParameterizedStatement { Statement = "DELETE FROM T" }]);
        await wrapper.ExecuteBatchWriteAsync(
            [new BatchStatementRequest { Statement = "DELETE FROM T" }]);

        interceptor
            .Events
            .Should()
            .Equal(
                "ExecuteStatementQuery:executing",
                "ExecuteStatementQuery:executed",
                "ExecuteStatementWrite:executing",
                "ExecuteStatementWrite:executed",
                "ExecuteTransaction:executing",
                "ExecuteTransaction:executed",
                "BatchExecuteStatement:executing",
                "BatchExecuteStatement:executed");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Intercepts_EachQueryPage_WithPageAndAttemptNumbers()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new ExecuteStatementResponse
                {
                    Items = [new Dictionary<string, AttributeValue>()], NextToken = "next"
                },
                new ExecuteStatementResponse { Items = [] });
        var interceptor = new RecordingInterceptor();
        await using var context = InterceptionContext.Create(client, interceptor);

        await foreach (var _ in context
            .GetService<IDynamoClientWrapper>()
            .ExecutePartiQl(new ExecuteStatementRequest { Statement = "SELECT * FROM T" })) { }

        interceptor.Executed.Should().HaveCount(2);
        interceptor
            .Executed
            .Select(e => (e.PageNumber, e.AttemptNumber))
            .Should()
            .Equal((1, 1), (2, 1));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Intercepts_FailedSdkCall_WithoutTreatingItAsCancellation()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<ExecuteStatementResponse>(
                    new InvalidOperationException("fail")));
        var interceptor = new RecordingInterceptor();
        await using var context = InterceptionContext.Create(client, interceptor);

        var act = async () =>
        {
            await foreach (var _ in context
                .GetService<IDynamoClientWrapper>()
                .ExecutePartiQl(new ExecuteStatementRequest { Statement = "SELECT * FROM T" })) { }
        };

        await act.Should().ThrowAsync<InvalidOperationException>();
        interceptor
            .Events
            .Should()
            .Equal("ExecuteStatementQuery:executing", "ExecuteStatementQuery:failed");
        interceptor
            .Errors
            .Should()
            .ContainSingle()
            .Which
            .Exception
            .Should()
            .BeOfType<InvalidOperationException>();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Intercepts_CanceledSdkCall_WhenCallerTokenIsCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<ExecuteStatementResponse>(cancellation.Token));
        var interceptor = new RecordingInterceptor();
        await using var context = InterceptionContext.Create(client, interceptor);

        var act = async () =>
        {
            await foreach (var _ in context
                .GetService<IDynamoClientWrapper>()
                .ExecutePartiQl(new ExecuteStatementRequest { Statement = "SELECT * FROM T" })
                .WithCancellation(cancellation.Token)) { }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        interceptor
            .Events
            .Should()
            .Equal("ExecuteStatementQuery:executing", "ExecuteStatementQuery:canceled");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Orders_InjectedInterceptor_Before_OptionsInterceptor()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [] });
        var events = new List<string>();
        var injected = new RecordingInterceptor("injected", events);
        var configured = new RecordingInterceptor("configured", events);
        var services = new ServiceCollection()
            .AddEntityFrameworkDynamo()
            .AddScoped<IInterceptor>(_ => injected)
            .BuildServiceProvider();
        var options = new DbContextOptionsBuilder<InterceptionContext>()
            .UseDynamo(options => options.DynamoDbClient(client))
            .UseInternalServiceProvider(services)
            .AddInterceptors(configured)
            .Options;
        await using var context = new InterceptionContext(options);

        await foreach (var _ in context
            .GetService<IDynamoClientWrapper>()
            .ExecutePartiQl(new ExecuteStatementRequest { Statement = "SELECT * FROM T" })) { }

        events
            .Should()
            .Equal(
                "injected:ExecuteStatementQuery:executing",
                "configured:ExecuteStatementQuery:executing",
                "injected:ExecuteStatementQuery:executed",
                "configured:ExecuteStatementQuery:executed");
    }

    private sealed class InterceptionContext(DbContextOptions<InterceptionContext> options)
        : DbContext(options)
    {
        public static InterceptionContext
            Create(IAmazonDynamoDB client, params IInterceptor[] interceptors)
            => new(
                new DbContextOptionsBuilder<InterceptionContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .AddInterceptors(interceptors)
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }

    private sealed class RecordingInterceptor(
        string? name = null,
        List<string>? orderedEvents = null) : DynamoDbCommandInterceptor
    {
        public List<string> Events { get; } = [];
        public List<DynamoDbCommandExecutedEventData> Executed { get; } = [];
        public List<DynamoDbCommandErrorEventData> Errors { get; } = [];

        public override ValueTask ExecuteStatementExecutingAsync(
            ExecuteStatementRequest request,
            DynamoDbCommandEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Record(eventData, "executing");
            return default;
        }

        public override ValueTask ExecuteStatementExecutedAsync(
            ExecuteStatementRequest request,
            DynamoDbCommandExecutedEventData eventData,
            ExecuteStatementResponse response,
            CancellationToken cancellationToken = default)
        {
            Executed.Add(eventData);
            Record(eventData, "executed");
            return default;
        }

        public override Task ExecuteStatementCanceledAsync(
            ExecuteStatementRequest request,
            DynamoDbCommandEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Record(eventData, "canceled");
            return Task.CompletedTask;
        }

        public override Task ExecuteStatementFailedAsync(
            ExecuteStatementRequest request,
            DynamoDbCommandErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Errors.Add(eventData);
            Record(eventData, "failed");
            return Task.CompletedTask;
        }

        public override ValueTask ExecuteTransactionExecutingAsync(
            ExecuteTransactionRequest request,
            DynamoDbCommandEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Record(eventData, "executing");
            return default;
        }

        public override ValueTask ExecuteTransactionExecutedAsync(
            ExecuteTransactionRequest request,
            DynamoDbCommandExecutedEventData eventData,
            ExecuteTransactionResponse response,
            CancellationToken cancellationToken = default)
        {
            Executed.Add(eventData);
            Record(eventData, "executed");
            return default;
        }

        public override ValueTask BatchExecuteStatementExecutingAsync(
            BatchExecuteStatementRequest request,
            DynamoDbCommandEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Record(eventData, "executing");
            return default;
        }

        public override ValueTask BatchExecuteStatementExecutedAsync(
            BatchExecuteStatementRequest request,
            DynamoDbCommandExecutedEventData eventData,
            BatchExecuteStatementResponse response,
            CancellationToken cancellationToken = default)
        {
            Executed.Add(eventData);
            Record(eventData, "executed");
            return default;
        }

        private void Record(DynamoDbCommandEventData eventData, string phase)
        {
            var value = $"{eventData.Operation}:{phase}";
            Events.Add(value);
            if (name is not null)
                orderedEvents!.Add($"{name}:{value}");
        }
    }
}
