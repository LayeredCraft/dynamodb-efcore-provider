using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Diagnostics;

/// <summary>Tests exception capture for DynamoDB failure logs.</summary>
public class DynamoFailureLoggingTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteStatementFailed_PassesExceptionToILogger()
    {
        var exception = new InvalidOperationException("read failed");
        var capture = new CapturingLoggerFactory();
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ExecuteStatementResponse>(exception));
        await using var context = RequestContext.Create(client, capture);
        var wrapper = context.GetService<IDynamoClientWrapper>();

        var act = async () =>
        {
            await foreach (var _ in wrapper.ExecutePartiQl(
                new ExecuteStatementRequest { Statement = "SELECT * FROM T", Limit = 10 })) { }
        };

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("read failed");
        var entry =
            capture
                .Entries
                .Should()
                .ContainSingle(e => e.EventId.Id == DynamoEventId.ExecuteStatementFailed.Id)
                .Subject;
        entry.LogLevel.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(exception);
        entry.State["requestId"].Should().BeNull();
        entry.State["elapsedMilliseconds"].Should().BeOfType<double>();
        entry.Message.Should().Contain("Failed executing DynamoDB ExecuteStatement request");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task PartiQlWriteRequestFailed_PassesExceptionToILogger()
    {
        var exception = new InvalidOperationException("write failed");
        var capture = new CapturingLoggerFactory();
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ExecuteStatementResponse>(exception));
        await using var context = RequestContext.Create(client, capture);
        var wrapper = context.GetService<IDynamoClientWrapper>();

        var act = () => wrapper.ExecuteWriteAsync("DELETE FROM T", []);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("write failed");
        var entry =
            capture
                .Entries
                .Should()
                .ContainSingle(e => e.EventId.Id == DynamoEventId.PartiQlWriteRequestFailed.Id)
                .Subject;
        entry.LogLevel.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(exception);
        entry.State["operation"].Should().Be("ExecuteStatement");
        entry.State["statementCount"].Should().Be(1);
        entry.State["requestId"].Should().BeNull();
        entry.State["elapsedMilliseconds"].Should().BeOfType<double>();
        entry.Message.Should().Contain("Failed executing DynamoDB ExecuteStatement write request");
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
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        private readonly List<LogEntry> _entries = [];

        public IReadOnlyList<LogEntry> Entries => _entries;

        public ILogger CreateLogger(string categoryName)
            => categoryName.StartsWith(
                DbLoggerCategory.Database.Command.Name,
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
