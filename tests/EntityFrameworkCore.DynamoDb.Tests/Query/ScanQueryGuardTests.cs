using System.Diagnostics;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Unit tests for scan-like query protection.</summary>
public class ScanQueryGuardTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task MissingPartitionKeyEquality_ThrowsByDefault_BeforeAwsCall()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);

        var act = async ()
            => await context
                .Items
                .Where(x => x.Status == "OPEN")
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Scan-like DynamoDB query detected*missing equality or IN predicate*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task HasKeyOnlyPartitionKeyEquality_Executes()
    {
        var client = CreateClient();
        await using var context = HasKeyScanGuardDbContext.Create(client);

        await context
            .Items
            .Where(x => x.Pk == "P#1")
            .ToListAsync(TestContext.Current.CancellationToken);

        await client.ReceivedWithAnyArgs(1).ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task HasKeyOnlySortKeyOnlyPredicate_ThrowsByDefault()
    {
        var client = CreateClient();
        await using var context = HasKeyScanGuardDbContext.Create(client);

        var act = async ()
            => await context
                .Items
                .Where(x => x.Sk == "S#1")
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Scan-like DynamoDB query detected*missing equality or IN predicate*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task PartitionKeyEquality_Executes()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);

        await context
            .Items
            .Where(x => x.Pk == "P#1")
            .ToListAsync(TestContext.Current.CancellationToken);

        await client.ReceivedWithAnyArgs(1).ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task PartitionKeyEquality_WithSingleSortKeyCondition_Executes()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);

        await context
            .Items
            .Where(x => x.Pk == "P#1" && x.Sk.CompareTo("S#1") >= 0)
            .ToListAsync(TestContext.Current.CancellationToken);

        await client.ReceivedWithAnyArgs(1).ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task NonKeyOr_WithPartitionKeyEquality_Executes()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);

        await context
            .Items
            .Where(x => x.Pk == "P#1" && (x.Status == "OPEN" || x.Total > 10))
            .ToListAsync(TestContext.Current.CancellationToken);

        await client.ReceivedWithAnyArgs(1).ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task PartitionKeyIn_ExecutesByDefault()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);
        var keys = new[] { "P#1", "P#2" };

        await context
            .Items
            .Where(x => keys.Contains(x.Pk))
            .ToListAsync(TestContext.Current.CancellationToken);

        await client.ReceivedWithAnyArgs(1).ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task PartitionKeyOr_ThrowsByDefault()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);

        var act = async ()
            => await context
                .Items
                .Where(x => x.Pk == "P#1" || x.Pk == "P#2")
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*OR predicate references*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SortKeyOr_ThrowsByDefault()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);

        var act = async ()
            => await context
                .Items
                .Where(x => x.Pk == "P#1" && (x.Sk == "S#1" || x.Sk == "S#2"))
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*OR predicate references*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SortKeyIn_ThrowsByDefault()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);
        var sortKeys = new[] { "S#1", "S#2" };

        var act = async ()
            => await context
                .Items
                .Where(x => x.Pk == "P#1" && sortKeys.Contains(x.Sk))
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*sort key 'sk' is used as a filter*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AllowScan_SuppressesThrow()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);

        await context
            .Items
            .Where(x => x.Status == "OPEN")
            .AllowScan()
            .ToListAsync(TestContext.Current.CancellationToken);

        await client.ReceivedWithAnyArgs(1).ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ConfigureWarnings_Log_Executes()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(
            client,
            w => w.Log(DynamoEventId.ScanLikeQueryDetected));

        await context
            .Items
            .Where(x => x.Status == "OPEN")
            .ToListAsync(TestContext.Current.CancellationToken);

        await client.ReceivedWithAnyArgs(1).ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task DiagnosticListener_Receives_Enriched_ExecuteStatement_EventData()
    {
        using var observer = new DynamoDiagnosticObserver();
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);

        await context
            .Items
            .Where(x => x.Pk == "P#1")
            .ToListAsync(TestContext.Current.CancellationToken);

        observer
            .Events
            .Any(e => e.Key == DynamoEventId.ExecutingExecuteStatement.Name
                && e.Value is DynamoExecuteStatementEventData { CommandId: var commandId }
                && commandId != Guid.Empty)
            .Should()
            .BeTrue();

        observer
            .Events
            .Any(e => e.Key == DynamoEventId.ExecutedExecuteStatement.Name
                && e.Value is DynamoExecuteStatementExecutedEventData
                {
                    CommandId: var commandId, Elapsed: var elapsed, RequestId: "request-1"
                }
                && commandId != Guid.Empty
                && elapsed >= TimeSpan.Zero)
            .Should()
            .BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task DiagnosticListener_Receives_Executed_Event_Per_Page()
    {
        using var observer = new DynamoDiagnosticObserver();
        var client = CreateClient(
            new ExecuteStatementResponse
            {
                Items = [CreateItem("S#1")],
                NextToken = "next",
                ResponseMetadata =
                    new Amazon.Runtime.ResponseMetadata { RequestId = "request-1" }
            },
            new ExecuteStatementResponse
            {
                Items = [CreateItem("S#2")],
                ResponseMetadata =
                    new Amazon.Runtime.ResponseMetadata { RequestId = "request-2" }
            });
        await using var context = ScanGuardDbContext.Create(client);

        await context
            .Items
            .Where(x => x.Pk == "P#1")
            .ToListAsync(TestContext.Current.CancellationToken);

        observer
            .Events
            .Count(e => e.Key == DynamoEventId.ExecutedExecuteStatement.Name
                && e.Value is DynamoExecuteStatementExecutedEventData
                {
                    RequestId: "request-1" or "request-2"
                })
            .Should()
            .Be(2);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task DiagnosticListener_Receives_Failed_ExecuteStatement_EventData()
    {
        using var observer = new DynamoDiagnosticObserver();
        var client = CreateClient();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<ExecuteStatementResponse>>(_
                => throw new InternalServerErrorException("boom") { RequestId = "failed-request" });
        await using var context = ScanGuardDbContext.Create(client);

        var act = async ()
            => await context
                .Items
                .Where(x => x.Pk == "P#1")
                .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InternalServerErrorException>();
        observer
            .Events
            .Any(e => e.Key == DynamoEventId.ExecuteStatementFailed.Name
                && e.Value is DynamoExecuteStatementFailedEventData
                {
                    RequestId: "failed-request", Exception: InternalServerErrorException
                })
            .Should()
            .BeTrue();
        observer.Events.Any(e => e.Key == CoreEventId.QueryIterationFailed.Name).Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task DiagnosticListener_Receives_QueryCanceled()
    {
        using var observer = new DynamoDiagnosticObserver();
        var client = CreateClient();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<ExecuteStatementResponse>>(callInfo
                => throw new OperationCanceledException(callInfo.Arg<CancellationToken>()));
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await using var context = ScanGuardDbContext.Create(client);

        var act = async () => await context.Items.Where(x => x.Pk == "P#1").ToListAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        observer.Events.Any(e => e.Key == CoreEventId.QueryCanceled.Name).Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task DiagnosticListener_Receives_ScanLikeQueryDetected_EventData()
    {
        using var observer = new DynamoDiagnosticObserver();
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(
            client,
            w => w.Log(DynamoEventId.ScanLikeQueryDetected));

        await context
            .Items
            .Where(x => x.Status == "OPEN")
            .ToListAsync(TestContext.Current.CancellationToken);

        observer
            .Events
            .Any(e => e.Key == DynamoEventId.ScanLikeQueryDetected.Name
                && e.Value is DynamoQueryDiagnosticEventData data
                && data.Message.Contains(
                    "Scan-like DynamoDB query detected",
                    StringComparison.Ordinal))
            .Should()
            .BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ConfigureWarnings_Ignore_Executes()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(
            client,
            w => w.Ignore(DynamoEventId.ScanLikeQueryDetected));

        await context
            .Items
            .Where(x => x.Status == "OPEN")
            .ToListAsync(TestContext.Current.CancellationToken);

        await client.ReceivedWithAnyArgs(1).ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ConfigureWarnings_DefaultLog_StillThrows()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(
            client,
            w => w.Default(WarningBehavior.Log));

        var act = async ()
            => await context
                .Items
                .Where(x => x.Status == "OPEN")
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Scan-like DynamoDB query detected*missing equality or IN predicate*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ConfigureWarnings_DefaultIgnore_StillThrows()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(
            client,
            w => w.Default(WarningBehavior.Ignore));

        var act = async ()
            => await context
                .Items
                .Where(x => x.Status == "OPEN")
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Scan-like DynamoDB query detected*missing equality or IN predicate*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    private static IAmazonDynamoDB CreateClient(params ExecuteStatementResponse[] responses)
    {
        var client = Substitute.For<IAmazonDynamoDB>();

        responses = responses.Length == 0
            ?
            [
                new ExecuteStatementResponse
                {
                    Items = [],
                    NextToken = null,
                    ResponseMetadata =
                        new Amazon.Runtime.ResponseMetadata { RequestId = "request-1" }
                }
            ]
            : responses;

        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(responses[0], responses.Skip(1).ToArray());

        return client;
    }

    private static Dictionary<string, AttributeValue> CreateItem(string sk)
        => new()
        {
            ["pk"] = new AttributeValue("P#1"),
            ["sk"] = new AttributeValue(sk),
            ["status"] = new AttributeValue("OPEN"),
            ["total"] = new AttributeValue { N = "1" }
        };

    private sealed class DynamoDiagnosticObserver
        : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly IDisposable _subscription;
        private IDisposable? _listenerSubscription;

        public DynamoDiagnosticObserver()
            => _subscription = DiagnosticListener.AllListeners.Subscribe(this);

        public List<KeyValuePair<string, object?>> Events { get; } = [];

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == DbLoggerCategory.Name)
                _listenerSubscription = value.Subscribe(this);
        }

        public void OnNext(KeyValuePair<string, object?> value) => Events.Add(value);

        public void OnError(Exception error) { }

        public void OnCompleted() { }

        public void Dispose()
        {
            _listenerSubscription?.Dispose();
            _subscription.Dispose();
        }
    }

    private sealed record ScanGuardItem
    {
        public string Pk { get; set; } = null!;

        public string Sk { get; set; } = null!;

        public string Status { get; set; } = null!;

        public int Total { get; set; }
    }

    private sealed class ScanGuardDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ScanGuardItem> Items => Set<ScanGuardItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ScanGuardItem>(b =>
            {
                b.ToTable("ScanGuardTable");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
            });

        public static ScanGuardDbContext Create(
            IAmazonDynamoDB client,
            Action<WarningsConfigurationBuilder>? configureWarnings = null)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ScanGuardDbContext>()
                .UseDynamo(options => options.DynamoDbClient(client))
                .ConfigureWarnings(w =>
                {
                    w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                    configureWarnings?.Invoke(w);
                });

            return new ScanGuardDbContext(optionsBuilder.Options);
        }
    }

    private sealed class HasKeyScanGuardDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ScanGuardItem> Items => Set<ScanGuardItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ScanGuardItem>(b =>
            {
                b.ToTable("HasKeyScanGuardTable");
                b.HasKey(x => new { x.Pk, x.Sk });
            });

        public static HasKeyScanGuardDbContext Create(IAmazonDynamoDB client)
        {
            var optionsBuilder = new DbContextOptionsBuilder<HasKeyScanGuardDbContext>()
                .UseDynamo(options => options.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

            return new HasKeyScanGuardDbContext(optionsBuilder.Options);
        }
    }
}
