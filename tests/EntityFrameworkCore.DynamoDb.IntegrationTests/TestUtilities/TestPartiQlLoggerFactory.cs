using AwesomeAssertions.Execution;
using EntityFrameworkCore.DynamoDb.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;

/// <summary>Represents the TestPartiQlLoggerFactory type.</summary>
public sealed class TestPartiQlLoggerFactory : ILoggerFactory
{
    private readonly TestPartiQlLogger _logger = new();
    private bool _disposed;

    /// <summary>Provides functionality for this member.</summary>
    public IReadOnlyList<string> PartiQlStatements => _logger.PartiQlStatements;

    /// <summary>Provides functionality for this member.</summary>
    public IReadOnlyList<ExecuteStatementCall> ExecuteStatementCalls
        => _logger.ExecuteStatementCalls;

    /// <summary>Provides functionality for this member.</summary>
    public IReadOnlyList<int> RowLimitingWarnings => _logger.RowLimitingWarnings;

    /// <summary>Provides functionality for this member.</summary>
    public IReadOnlyList<QueryDiagnosticEvent> QueryDiagnosticEvents
        => _logger.QueryDiagnosticEvents;

    /// <summary>Provides functionality for this member.</summary>
    public void Clear() => _logger.Clear();

    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    public void AddProvider(ILoggerProvider provider)
        => ObjectDisposedException.ThrowIf(_disposed, typeof(TestPartiQlLoggerFactory));

    /// <summary>Provides functionality for this member.</summary>
    public void Dispose() => _disposed = true;

    /// <summary>Provides functionality for this member.</summary>
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
            {
                actualNormalized[i]
                    .Should()
                    .Be(expectedNormalized[i], $"PartiQL baseline mismatch at index {i}.");
            }
        }
        finally
        {
            Clear();
        }
    }

    private string BuildActualBaseline()
        => "Actual PartiQL baseline:"
            + Environment.NewLine
            + string.Join(
                Environment.NewLine + Environment.NewLine,
                PartiQlStatements.Select(s
                    => "\"\"\"" + Environment.NewLine + s + Environment.NewLine + "\"\"\""));

    private sealed class TestPartiQlLogger : ILogger
    {
        /// <summary>Provides functionality for this member.</summary>
        public List<string> PartiQlStatements { get; } = [];

        /// <summary>Provides functionality for this member.</summary>
        public List<ExecuteStatementCall> ExecuteStatementCalls { get; } = [];

        /// <summary>Provides functionality for this member.</summary>
        public List<int> RowLimitingWarnings { get; } = [];

        /// <summary>Provides functionality for this member.</summary>
        public List<QueryDiagnosticEvent> QueryDiagnosticEvents { get; } = [];

        /// <summary>Provides functionality for this member.</summary>
        public void Clear()
        {
            PartiQlStatements.Clear();
            ExecuteStatementCalls.Clear();
            RowLimitingWarnings.Clear();
            QueryDiagnosticEvents.Clear();
        }

        /// <summary>Provides functionality for this member.</summary>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Provides functionality for this member.</summary>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <summary>Provides functionality for this member.</summary>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (eventId.Id == DynamoEventId.ExecutingPartiQlQuery.Id
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

            if (eventId.Id == DynamoEventId.RowLimitingQueryWithoutPageSize.Id
                && state is IReadOnlyList<KeyValuePair<string, object?>> rowLimitingStructure)
            {
                var resultLimit =
                    rowLimitingStructure
                        .Where(i => i.Key == "resultLimit")
                        .Select(i => ToNullableInt(i.Value))
                        .FirstOrDefault();

                if (resultLimit.HasValue)
                    RowLimitingWarnings.Add(resultLimit.Value);
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

    /// <summary>Represents the ExecuteStatementCall type.</summary>
    public sealed record ExecuteStatementCall(
        int? Limit,
        bool RequestNextTokenPresent,
        int? ItemsCount,
        bool? ResponseNextTokenPresent);

    /// <summary>Represents the QueryDiagnosticEvent type.</summary>
    public sealed record QueryDiagnosticEvent(EventId EventId, LogLevel LogLevel, string Message);
}
