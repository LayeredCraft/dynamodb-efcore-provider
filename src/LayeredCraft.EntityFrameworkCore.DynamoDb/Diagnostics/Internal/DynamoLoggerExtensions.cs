using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Diagnostics.Internal;

public static class DynamoLoggerExtensions
{
    private static readonly Action<ILogger, string, string, string, Exception?>
        LogExecutingPartiQlQuery = LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            DynamoEventId.ExecutingPartiQlQuery,
            "Executing DynamoDB PartiQL query for table '{tableName}'{newLine}{commandText}");

    private static readonly Action<ILogger, int?, bool, Exception?> LogExecutingExecuteStatement =
        LoggerMessage.Define<int?, bool>(
            LogLevel.Information,
            DynamoEventId.ExecutingExecuteStatement,
            "Executing DynamoDB ExecuteStatement request (limit: {limit}, nextTokenPresent: {nextTokenPresent})");

    private static readonly Action<ILogger, int, bool, Exception?> LogExecutedExecuteStatement =
        LoggerMessage.Define<int, bool>(
            LogLevel.Information,
            DynamoEventId.ExecutedExecuteStatement,
            "Executed DynamoDB ExecuteStatement request (itemsCount: {itemsCount}, nextTokenPresent: {nextTokenPresent})");

    private static readonly Action<ILogger, int, Exception?> LogRowLimitingQueryWithoutPageSize =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            DynamoEventId.RowLimitingQueryWithoutPageSize,
            "Executing a row-limiting query (resultLimit: {resultLimit}) without a configured page size. Configure DefaultPageSize or use WithPageSize(...) to control per-request evaluated items.");

    public static void ExecutingPartiQlQuery(
        this IDiagnosticsLogger<DbLoggerCategory.Database.Command> diagnostics,
        string tableName,
        string commandText)
    {
        if (!diagnostics.Logger.IsEnabled(LogLevel.Information))
            return;

        LogExecutingPartiQlQuery(
            diagnostics.Logger,
            tableName,
            Environment.NewLine,
            commandText,
            null);
    }

    public static void ExecutingExecuteStatement(
        this IDiagnosticsLogger<DbLoggerCategory.Database.Command> diagnostics,
        int? limit,
        bool nextTokenPresent)
    {
        if (!diagnostics.Logger.IsEnabled(LogLevel.Information))
            return;

        LogExecutingExecuteStatement(diagnostics.Logger, limit, nextTokenPresent, null);
    }

    public static void ExecutedExecuteStatement(
        this IDiagnosticsLogger<DbLoggerCategory.Database.Command> diagnostics,
        int itemsCount,
        bool nextTokenPresent)
    {
        if (!diagnostics.Logger.IsEnabled(LogLevel.Information))
            return;

        LogExecutedExecuteStatement(diagnostics.Logger, itemsCount, nextTokenPresent, null);
    }

    public static void RowLimitingQueryWithoutPageSize(
        this IDiagnosticsLogger<DbLoggerCategory.Database.Command> diagnostics,
        int resultLimit)
    {
        if (!diagnostics.Logger.IsEnabled(LogLevel.Warning))
            return;

        LogRowLimitingQueryWithoutPageSize(diagnostics.Logger, resultLimit, null);
    }
}
