using System.Text;
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
                    BuildBaselineFailureMessage(normalizedExpected, actual));
        }
        finally
        {
            Clear();
        }
    }

    private static string BuildBaselineFailureMessage(
        IReadOnlyList<string> expected,
        IReadOnlyList<string> actual)
    {
        var builder = new StringBuilder()
            .AppendLine("PartiQL baseline mismatch.")
            .AppendLine($"Expected statement count: {expected.Count}")
            .AppendLine($"Actual statement count:   {actual.Count}");

        var compareCount = Math.Min(expected.Count, actual.Count);
        for (var i = 0; i < compareCount; i++)
        {
            if (expected[i] == actual[i])
                continue;

            AppendStatementDiff(builder, i, expected[i], actual[i]);
            break;
        }

        if (expected.Count != actual.Count)
        {
            builder.AppendLine();
            builder.AppendLine("All statements:");
            AppendStatementList(builder, "Expected", expected);
            AppendStatementList(builder, "Actual", actual);
        }

        return builder.ToString();
    }

    private static void AppendStatementDiff(
        StringBuilder builder,
        int index,
        string expected,
        string actual)
    {
        builder.AppendLine();
        builder.AppendLine($"First mismatch at statement index {index}:");
        builder.AppendLine("Expected:");
        AppendIndented(builder, expected);
        builder.AppendLine("Actual:");
        AppendIndented(builder, actual.Length == 0 ? "<empty>" : actual);

        var mismatchIndex = FirstMismatchIndex(expected, actual);
        builder.AppendLine($"First differing character: {mismatchIndex}");
        builder.AppendLine($"Expected from there: {Snippet(expected, mismatchIndex)}");
        builder.AppendLine($"Actual from there:   {Snippet(actual, mismatchIndex)}");
    }

    private static void AppendStatementList(
        StringBuilder builder,
        string title,
        IReadOnlyList<string> statements)
    {
        builder.AppendLine($"{title}:");

        if (statements.Count == 0)
        {
            builder.AppendLine("  <none>");
            return;
        }

        for (var i = 0; i < statements.Count; i++)
        {
            builder.AppendLine($"  [{i}]");
            AppendIndented(builder, statements[i], "    ");
        }
    }

    private static void AppendIndented(StringBuilder builder, string value, string indent = "  ")
    {
        foreach (var line in value.Split('\n'))
            builder.Append(indent).AppendLine(line);
    }

    private static int FirstMismatchIndex(string expected, string actual)
    {
        var length = Math.Min(expected.Length, actual.Length);
        for (var i = 0; i < length; i++)
            if (expected[i] != actual[i])
                return i;

        return length;
    }

    private static string Snippet(string value, int start)
    {
        if (start >= value.Length)
            return "<end>";

        var length = Math.Min(80, value.Length - start);
        return value.Substring(start, length).Replace("\n", "\\n");
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
