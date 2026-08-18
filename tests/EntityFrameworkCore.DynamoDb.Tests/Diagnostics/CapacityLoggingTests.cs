using System.Diagnostics;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using DynamoDbLoggerCategory = Microsoft.EntityFrameworkCore.DynamoDB.DbLoggerCategory;

namespace EntityFrameworkCore.DynamoDb.Tests.Diagnostics;

/// <summary>Tests that consumed-capacity (RCU/WCU) events are emitted into the Capacity category.</summary>
public class CapacityLoggingTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Read_EmitConsumedCapacityEvent()
    {
        var capture = new CapturingLoggerFactory();
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    new ExecuteStatementResponse
                    {
                        ConsumedCapacity = new ConsumedCapacity
                        {
                            TableName = "Test", CapacityUnits = 2.5
                        }
                    }));

        await using var context = RequestContext.Create(client, capture);
        var wrapper = context.GetService<IDynamoClientWrapper>();

        await foreach (var _ in wrapper.ExecutePartiQl(
            new ExecuteStatementRequest { Statement = "SELECT * FROM Test" })) { }

        var entry =
            capture
                .Entries
                .Should()
                .ContainSingle(e => e.EventId.Id == DynamoEventId.ConsumedCapacity.Id)
                .Subject;
        entry.LogLevel.Should().Be(LogLevel.Information);
        entry.State["capacityUnits"].Should().Be(2.5);
        entry.State["entryCount"].Should().Be(1);
        entry.Message.Should().Contain("2.5");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Write_EmitConsumedCapacityEvent()
    {
        var capture = new CapturingLoggerFactory();
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    new ExecuteStatementResponse
                    {
                        ConsumedCapacity = new ConsumedCapacity
                        {
                            TableName = "Test", CapacityUnits = 4.0
                        }
                    }));

        await using var context = RequestContext.Create(client, capture);
        var wrapper = context.GetService<IDynamoClientWrapper>();

        await wrapper.ExecuteWriteAsync("INSERT INTO \"Test\" VALUE {'pk': 'a'}", []);

        var entry =
            capture
                .Entries
                .Should()
                .ContainSingle(e => e.EventId.Id == DynamoEventId.ConsumedCapacity.Id)
                .Subject;
        entry.LogLevel.Should().Be(LogLevel.Information);
        entry.State["capacityUnits"].Should().Be(4.0);
        entry.State["entryCount"].Should().Be(1);
        entry.Message.Should().Contain("4");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task NoConsumedCapacity_EmitsNothing()
    {
        var capture = new CapturingLoggerFactory();
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ExecuteStatementResponse()));

        await using var context = RequestContext.Create(client, capture);
        var wrapper = context.GetService<IDynamoClientWrapper>();

        await foreach (var _ in wrapper.ExecutePartiQl(
            new ExecuteStatementRequest { Statement = "SELECT * FROM Test" })) { }

        capture.Entries.Should().NotContain(e => e.EventId.Id == DynamoEventId.ConsumedCapacity.Id);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ConsumedCapacity_CommandId_MatchesConsumingCommand()
    {
        using var observer = new DynamoDiagnosticObserver();
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    new ExecuteStatementResponse
                    {
                        ConsumedCapacity = new ConsumedCapacity
                        {
                            TableName = "Test", CapacityUnits = 1.5
                        }
                    }));

        await using var context = RequestContext.Create(client);
        var wrapper = context.GetService<IDynamoClientWrapper>();

        await foreach (var _ in wrapper.ExecutePartiQl(
            new ExecuteStatementRequest { Statement = "SELECT * FROM Test" })) { }

        // The DiagnosticListener is process-global, so other tests running in
        // parallel may contribute events. Assert the correlation property rather
        // than an exact count: every capacity event (this operation's is the one
        // with 1.5 units) must share its CommandId with a command event.
        var capacityCommandIds =
            observer
                .Snapshot()
                .Where(e => e.Key == DynamoEventId.ConsumedCapacity.Name
                    && e.Value is DynamoConsumedCapacityEventData { CapacityUnits: 1.5 })
                .Select(e => ((DynamoConsumedCapacityEventData)e.Value!).CommandId)
                .ToList();

        capacityCommandIds.Should().NotBeEmpty();

        var executedCommandIds =
            observer
                .Snapshot()
                .Where(e => e.Key == DynamoEventId.ExecutedExecuteStatement.Name)
                .Select(e => ((DynamoExecuteStatementExecutedEventData)e.Value!).CommandId)
                .ToHashSet();

        executedCommandIds.Should().Contain(capacityCommandIds);
    }

    private sealed class RequestContext(DbContextOptions<RequestContext> options) : DbContext(
        options)
    {
        public static RequestContext Create(IAmazonDynamoDB client, ILoggerFactory loggerFactory)
        {
            var optionsBuilder = new DbContextOptionsBuilder<RequestContext>();
            optionsBuilder
                .UseDynamo(options => options.DynamoDbClient(client))
                .UseLoggerFactory(loggerFactory)
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

            return new RequestContext(optionsBuilder.Options);
        }

        public static RequestContext Create(IAmazonDynamoDB client)
        {
            var optionsBuilder = new DbContextOptionsBuilder<RequestContext>();
            optionsBuilder
                .UseDynamo(options => options.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

            return new RequestContext(optionsBuilder.Options);
        }
    }

    private sealed class DynamoDiagnosticObserver
        : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly IDisposable _subscription;
        private readonly object _gate = new();
        private IDisposable? _listenerSubscription;
        private readonly List<KeyValuePair<string, object?>> _events = [];

        public DynamoDiagnosticObserver()
            => _subscription = DiagnosticListener.AllListeners.Subscribe(this);

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == DbLoggerCategory.Name)
                _listenerSubscription = value.Subscribe(this);
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            lock (_gate)
            {
                _events.Add(value);
            }
        }

        public IReadOnlyList<KeyValuePair<string, object?>> Snapshot()
        {
            lock (_gate)
            {
                return _events.ToList();
            }
        }

        public void OnError(Exception error) { }

        public void OnCompleted() { }

        public void Dispose()
        {
            _listenerSubscription?.Dispose();
            _subscription.Dispose();
        }
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        private readonly List<LogEntry> _entries = [];

        public IReadOnlyList<LogEntry> Entries => _entries;

        public ILogger CreateLogger(string categoryName)
            => categoryName.StartsWith(
                DynamoDbLoggerCategory.Capacity.Name,
                StringComparison.Ordinal)
                ? new CapturingLogger(this)
                : NullLogger.Instance;

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }

        private void Add(LogEntry entry) => _entries.Add(entry);

        private sealed class CapturingLogger(CapturingLoggerFactory factory) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var structuredState = state is IReadOnlyList<KeyValuePair<string, object?>> values
                    ? values
                        .Where(pair => pair.Key != "{OriginalFormat}")
                        .ToDictionary(pair => pair.Key, pair => pair.Value)
                    : [];

                factory.Add(
                    new LogEntry(
                        logLevel,
                        eventId,
                        exception,
                        formatter(state, exception),
                        structuredState));
            }
        }
    }

    private sealed record LogEntry(
        LogLevel LogLevel,
        EventId EventId,
        Exception? Exception,
        string Message,
        IReadOnlyDictionary<string, object?> State);
}
