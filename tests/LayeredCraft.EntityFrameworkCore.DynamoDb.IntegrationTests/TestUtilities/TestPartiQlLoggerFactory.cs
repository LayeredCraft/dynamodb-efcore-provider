using LayeredCraft.EntityFrameworkCore.DynamoDb.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;

public sealed class TestPartiQlLoggerFactory : ILoggerFactory
{
    private static readonly string Eol = Environment.NewLine;

    private readonly TestPartiQlLogger _logger = new();
    private bool _disposed;

    public IReadOnlyList<string> PartiQlStatements => _logger.PartiQlStatements;

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
            if (PartiQlStatements.Count != expected.Length)
                throw new InvalidOperationException(
                    $"Expected {expected.Length} PartiQL statement(s) but captured {PartiQlStatements.Count}."
                    + Environment.NewLine
                    + BuildActualBaseline());

            for (var i = 0; i < expected.Length; i++)
            {
                var expectedNormalized = expected[i].ReplaceLineEndings();
                var actualNormalized = PartiQlStatements[i].ReplaceLineEndings();
                if (!string.Equals(expectedNormalized, actualNormalized, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"PartiQL baseline mismatch at index {i}."
                        + Environment.NewLine
                        + BuildActualBaseline());
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

        public void Clear() => PartiQlStatements.Clear();

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
                var parameters =
                    structure
                        .Where(i => i.Key == "parameters")
                        .Select(i => (string?)i.Value)
                        .FirstOrDefault()
                    ?? string.Empty;
                var commandText =
                    structure
                        .Where(i => i.Key == "commandText")
                        .Select(i => (string?)i.Value)
                        .FirstOrDefault()
                    ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(parameters))
                    parameters =
                        parameters.Replace(", ", Eol, StringComparison.Ordinal) + Eol + Eol;
                else
                    parameters = string.Empty;

                PartiQlStatements.Add(parameters + commandText);
            }
        }
    }
}
