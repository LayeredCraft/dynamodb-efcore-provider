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
                + $"{MaxStatementLength}-byte statement-size limit.");
        }

        var byteCount = Encoding.UTF8.GetByteCount(statement);
        if (byteCount <= MaxStatementLength)
            return;

        throw new InvalidOperationException(
            $"The generated PartiQL {operation} statement is {byteCount} UTF-8 bytes, "
            + $"which exceeds DynamoDB's {MaxStatementLength}-byte statement-size limit.");
    }

    private static bool ContainsNonAscii(string value) => value.Any(static ch => ch > 0x7F);
}
