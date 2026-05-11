using EntityFrameworkCore.DynamoDb.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;

/// <summary>PartiQL assertion helper for DynamoDB specification tests.</summary>
public class TestSqlLoggerFactory(Func<string, bool> shouldLogCategory)
    : ListLoggerFactory(shouldLogCategory)
{
    public TestSqlLoggerFactory() : this(_ => true) { }

    public IReadOnlyList<string> SqlStatements
        => Log
            .Where(e => e.Id.Id is var id && id == DynamoEventId.ExecutingPartiQlQuery.Id)
            .Select(e => GetCommandText(e.State))
            .ToArray();

    public void AssertBaseline(params string[] expected)
    {
        try
        {
            var actual = SqlStatements.Select(sql => sql.ReplaceLineEndings()).ToArray();
            var normalizedExpected = expected.Select(sql => sql.ReplaceLineEndings()).ToArray();

            if (!actual.SequenceEqual(normalizedExpected))
                throw new InvalidOperationException(
                    $"Expected SQL: {string.Join(" | ", normalizedExpected)} Actual SQL: {string.Join(" | ", actual)}");
        }
        finally
        {
            Clear();
        }
    }

    private static string GetCommandText(object? state)
        => state is IReadOnlyList<KeyValuePair<string, object?>> structure
            ? structure
                .Where(item => item.Key == "commandText")
                .Select(item => (string?)item.Value)
                .FirstOrDefault()
            ?? string.Empty
            : string.Empty;
}
