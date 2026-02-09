using AwesomeAssertions.Execution;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;

public sealed class TestPartiQlLoggerFactory : ILoggerFactory
{
    private readonly TestPartiQlLogger _logger = new();
    private bool _disposed;

    public IReadOnlyList<string> PartiQlStatements => _logger.PartiQlStatements;

    public IReadOnlyList<ExecuteStatementCall> ExecuteStatementCalls
        => _logger.ExecuteStatementCalls;

    public IReadOnlyList<int> RowLimitingWarnings => _logger.RowLimitingWarnings;

    public void Clear() => _logger.Clear();

    public ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(TestPartiQlLoggerFactory));

        return categoryName == DbLoggerCategory.Database.Command.Name
            ? _logger
            : NullLogger.Instance;
    }

    public void AddProvider(ILoggerProvider provider)
        => ObjectDisposedException.ThrowIf(_disposed, typeof(TestPartiQlLoggerFactory));

    public void Dispose() => _disposed = true;

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
        public List<string> PartiQlStatements { get; } = [];
        public List<ExecuteStatementCall> ExecuteStatementCalls { get; } = [];
        public List<int> RowLimitingWarnings { get; } = [];

        public void Clear()
        {
            PartiQlStatements.Clear();
            ExecuteStatementCalls.Clear();
            RowLimitingWarnings.Clear();
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
        }

        private static int? ToNullableInt(object? value)
        {
            if (value is int intValue)
                return intValue;

            return null;
        }
    }

    public sealed record ExecuteStatementCall(
        int? Limit,
        bool RequestNextTokenPresent,
        int? ItemsCount,
        bool? ResponseNextTokenPresent);
}
