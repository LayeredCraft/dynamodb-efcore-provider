using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Diagnostics.Internal;

public static class DynamoLoggerExtensions
{
    private static readonly Action<ILogger, string, string, string, string, Exception?>
        LogExecutingPartiQlQuery = LoggerMessage.Define<string, string, string, string>(
            LogLevel.Information,
            DynamoEventId.ExecutingPartiQlQuery,
            "Executing DynamoDB PartiQL query for table '{tableName}' [Parameters=[{parameters}]]{newLine}{commandText}");

    public static void ExecutingPartiQlQuery(
        this IDiagnosticsLogger<DbLoggerCategory.Database.Command> diagnostics,
        string tableName,
        string commandText,
        IReadOnlyList<AttributeValue> parameters)
    {
        if (!diagnostics.Logger.IsEnabled(LogLevel.Information))
            return;

        var formattedParameters =
            FormatParameters(parameters, diagnostics.ShouldLogSensitiveData());
        LogExecutingPartiQlQuery(
            diagnostics.Logger,
            tableName,
            formattedParameters,
            Environment.NewLine,
            commandText,
            null);
    }

    private static string FormatParameters(
        IReadOnlyList<AttributeValue> parameters,
        bool logSensitiveData)
    {
        if (parameters.Count == 0)
            return string.Empty;

        var parts = new string[parameters.Count];
        for (var i = 0; i < parameters.Count; i++)
            parts[i] = $"p{i}=" + (logSensitiveData ? FormatParameterValue(parameters[i]) : "?");

        return string.Join(", ", parts);
    }

    private static string FormatParameterValue(AttributeValue value)
    {
        if (value.NULL == true)
            return "null";

        if (value.S is not null)
            return "'" + value.S.Replace("'", "''", StringComparison.Ordinal) + "'";

        if (value.N is not null)
            return value.N;

        if (value.BOOL is not null)
            return value.BOOL.Value ? "TRUE" : "FALSE";

        // For MVP we only expect scalar parameters.
        return "?";
    }
}
