using System.Text;

namespace EntityFrameworkCore.DynamoDb.Storage;

internal static class DynamoPartiQlStatementValidator
{
    internal const int MaxStatementLength = 8192;

    public static void ValidateStatementLength(string statement, string operation)
    {
        if (!ContainsNonAscii(statement))
        {
            if (statement.Length <= MaxStatementLength)
                return;

            throw new InvalidOperationException(
                $"The generated PartiQL {operation} statement is {statement.Length} characters "
                + $"(ASCII-equivalent bytes), which exceeds DynamoDB's "
                + $"{MaxStatementLength}-byte statement-size limit. "
                + GetRemediation(operation));
        }

        var byteCount = Encoding.UTF8.GetByteCount(statement);
        if (byteCount <= MaxStatementLength)
            return;

        throw new InvalidOperationException(
            $"The generated PartiQL {operation} statement is {byteCount} UTF-8 bytes, "
            + $"which exceeds DynamoDB's {MaxStatementLength}-byte statement-size limit. "
            + GetRemediation(operation));
    }

    private static string GetRemediation(string operation)
        => operation switch
        {
            "write" => "Consider reducing the number of mapped scalar properties or splitting "
                + "the write unit across multiple SaveChanges calls.",
            "read" => "Consider narrowing the projection, simplifying the predicate, or splitting "
                + "the query into smaller requests.",
            _ => "Consider reducing statement complexity or splitting the operation into "
                + "smaller requests."
        };

    private static bool ContainsNonAscii(string value) => value.Any(static ch => ch > 0x7F);
}
