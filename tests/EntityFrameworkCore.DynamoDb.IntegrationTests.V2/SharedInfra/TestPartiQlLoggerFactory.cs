using AwesomeAssertions.Execution;
using EntityFrameworkCore.DynamoDb.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;

/// <summary>
///     Captures PartiQL statements and diagnostic events emitted by the DynamoDB EF Core provider
///     during a test. Wire it in via
///     <see cref="Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.UseLoggerFactory" />.
/// </summary>
public sealed class TestPartiQlLoggerFactory : ILoggerFactory
{
    private readonly TestPartiQlLogger _logger = new();
    private bool _disposed;

    /// <summary>PartiQL statements captured since the last <see cref="Clear" />.</summary>
    public IReadOnlyList<string> PartiQlStatements => _logger.PartiQlStatements;

    /// <summary>ExecuteStatement call metadata captured since the last <see cref="Clear" />.</summary>
    public IReadOnlyList<ExecuteStatementCall> ExecuteStatementCalls
        => _logger.ExecuteStatementCalls;

    /// <summary>Query diagnostic events (index selection) captured since the last <see cref="Clear" />.</summary>
    public IReadOnlyList<QueryDiagnosticEvent> QueryDiagnosticEvents
        => _logger.QueryDiagnosticEvents;

    /// <summary>Clears all captured state.</summary>
    public void Clear() => _logger.Clear();

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(TestPartiQlLoggerFactory));

        return categoryName.StartsWith(
                DbLoggerCategory.Database.Command.Name,
                StringComparison.Ordinal)
            || categoryName.StartsWith(DbLoggerCategory.Query.Name, StringComparison.Ordinal)
                ? _logger
                : NullLogger.Instance;
    }

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider)
        => ObjectDisposedException.ThrowIf(_disposed, typeof(TestPartiQlLoggerFactory));

    /// <inheritdoc />
    public void Dispose() => _disposed = true;

    /// <summary>
    ///     Asserts that the captured PartiQL statements match <paramref name="expected" />, then
    ///     clears the capture buffer so the next assertion starts clean.
    /// </summary>
    public void AssertBaseline(params string[] expected)
    {
        try
        {
            using var scope = new AssertionScope();

            var expectedNormalized = expected.Select(s => s.ReplaceLineEndings()).ToArray();
            var actualNormalized = PartiQlStatements.Select(s => s.ReplaceLineEndings()).ToArray();

            actualNormalized
                .Should()
                .HaveSameCount(
                    expectedNormalized,
                    $"Expected {expectedNormalized.Length} PartiQL statement(s) but captured {actualNormalized.Length}.");

            var compareCount = Math.Min(expectedNormalized.Length, actualNormalized.Length);
            for (var i = 0; i < compareCount; i++)
                actualNormalized[i]
                    .Should()
                    .Be(expectedNormalized[i], $"PartiQL baseline mismatch at index {i}.");
        }
        finally
        {
            Clear();
        }
    }

    private sealed class TestPartiQlLogger : ILogger
    {
        public List<string> PartiQlStatements { get; } = [];
        public List<ExecuteStatementCall> ExecuteStatementCalls { get; } = [];
        public List<QueryDiagnosticEvent> QueryDiagnosticEvents { get; } = [];

        public void Clear()
        {
            PartiQlStatements.Clear();
            ExecuteStatementCalls.Clear();
            QueryDiagnosticEvents.Clear();
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if ((eventId.Id == DynamoEventId.ExecutingPartiQlQuery.Id
                    || eventId.Id == DynamoEventId.ExecutingPartiQlWrite.Id)
                && state is IReadOnlyList<KeyValuePair<string, object?>> structure)
            {
                var commandText =
                    structure
                        .Where(i => i.Key == "commandText")
                        .Select(i => (string?)i.Value)
                        .FirstOrDefault()
                    ?? string.Empty;

                PartiQlStatements.Add(commandText);
            }

            if (eventId.Id == DynamoEventId.ExecutingExecuteStatement.Id
                && state is IReadOnlyList<KeyValuePair<string, object?>> executingStructure)
            {
                var limit =
                    executingStructure
                        .Where(i => i.Key == "limit")
                        .Select(i => ToNullableInt(i.Value))
                        .FirstOrDefault();

                var nextTokenPresent =
                    executingStructure
                        .Where(i => i.Key == "nextTokenPresent")
                        .Select(i => (bool?)i.Value)
                        .FirstOrDefault()
                    ?? false;

                ExecuteStatementCalls.Add(
                    new ExecuteStatementCall(limit, nextTokenPresent, null, null));
            }

            if (eventId.Id == DynamoEventId.ExecutedExecuteStatement.Id
                && state is IReadOnlyList<KeyValuePair<string, object?>> executedStructure)
            {
                var itemsCount =
                    executedStructure
                        .Where(i => i.Key == "itemsCount")
                        .Select(i => ToNullableInt(i.Value))
                        .FirstOrDefault();

                var nextTokenPresent =
                    executedStructure
                        .Where(i => i.Key == "nextTokenPresent")
                        .Select(i => (bool?)i.Value)
                        .FirstOrDefault();

                if (ExecuteStatementCalls.Count > 0)
                {
                    var lastIndex = ExecuteStatementCalls.Count - 1;
                    var existing = ExecuteStatementCalls[lastIndex];
                    ExecuteStatementCalls[lastIndex] = existing with
                    {
                        ItemsCount = itemsCount, ResponseNextTokenPresent = nextTokenPresent,
                    };
                }
            }

            if (eventId.Id == DynamoEventId.NoCompatibleSecondaryIndexFound.Id // IDX001
                || eventId.Id == DynamoEventId.MultipleCompatibleSecondaryIndexesFound.Id // IDX002
                || eventId.Id == DynamoEventId.SecondaryIndexSelected.Id // IDX003
                || eventId.Id == DynamoEventId.ExplicitIndexSelected.Id // IDX004
                || eventId.Id == DynamoEventId.SecondaryIndexCandidateRejected.Id // IDX005
                || eventId.Id == DynamoEventId.ExplicitIndexSelectionDisabled.Id) // IDX006
                QueryDiagnosticEvents.Add(
                    new QueryDiagnosticEvent(eventId, logLevel, formatter(state, exception)));
        }

        private static int? ToNullableInt(object? value)
        {
            if (value is int intValue)
                return intValue;

            return null;
        }
    }

    /// <summary>Metadata for a single <c>ExecuteStatement</c> API call.</summary>
    public sealed record ExecuteStatementCall(
        int? Limit,
        bool RequestNextTokenPresent,
        int? ItemsCount,
        bool? ResponseNextTokenPresent);

    /// <summary>A captured query diagnostic event (e.g. index selection).</summary>
    public sealed record QueryDiagnosticEvent(EventId EventId, LogLevel LogLevel, string Message);
}
