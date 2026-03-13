using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;
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
            "Executing a row-limiting query (resultLimit: {resultLimit}) without a configured page size. Configure DefaultPageSize or use WithPageSize(...) to control per-request evaluated items. Consider using FirstAsync(pageSize, ...) or FirstOrDefaultAsync(pageSize, ...).");

    private static readonly Action<ILogger, string, Exception?> LogNoCompatibleSecondaryIndexFound =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            DynamoEventId.NoCompatibleSecondaryIndexFound,
            "{message}");

    private static readonly Action<ILogger, string, Exception?>
        LogMultipleCompatibleSecondaryIndexesFound = LoggerMessage.Define<string>(
            LogLevel.Warning,
            DynamoEventId.MultipleCompatibleSecondaryIndexesFound,
            "{message}");

    private static readonly Action<ILogger, string, Exception?> LogSecondaryIndexSelected =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            DynamoEventId.SecondaryIndexSelected,
            "{message}");

    private static readonly Action<ILogger, string, Exception?> LogExplicitIndexSelected =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            DynamoEventId.ExplicitIndexSelected,
            "{message}");

    private static readonly Action<ILogger, string, Exception?> LogSecondaryIndexCandidateRejected =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            DynamoEventId.SecondaryIndexCandidateRejected,
            "{message}");

    private static readonly Action<ILogger, string, Exception?> LogExplicitIndexSelectionDisabled =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            DynamoEventId.ExplicitIndexSelectionDisabled,
            "{message}");

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

    /// <summary>Logs a structured query-compilation diagnostic from automatic index selection.</summary>
    /// <param name="diagnostics">The EF Core query diagnostics logger.</param>
    /// <param name="diagnostic">The provider diagnostic produced during index analysis.</param>
    internal static void IndexSelectionDiagnostic(
        this IDiagnosticsLogger<DbLoggerCategory.Query> diagnostics,
        DynamoQueryDiagnostic diagnostic)
    {
        switch (diagnostic.Code)
        {
            case "DYNAMO_IDX001":
                if (!diagnostics.Logger.IsEnabled(LogLevel.Warning))
                    return;

                LogNoCompatibleSecondaryIndexFound(diagnostics.Logger, diagnostic.Message, null);
                return;

            case "DYNAMO_IDX002":
                if (!diagnostics.Logger.IsEnabled(LogLevel.Warning))
                    return;

                LogMultipleCompatibleSecondaryIndexesFound(
                    diagnostics.Logger,
                    diagnostic.Message,
                    null);
                return;

            case "DYNAMO_IDX003":
                if (!diagnostics.Logger.IsEnabled(LogLevel.Information))
                    return;

                LogSecondaryIndexSelected(diagnostics.Logger, diagnostic.Message, null);
                return;

            case "DYNAMO_IDX004":
                if (!diagnostics.Logger.IsEnabled(LogLevel.Information))
                    return;

                LogExplicitIndexSelected(diagnostics.Logger, diagnostic.Message, null);
                return;

            case "DYNAMO_IDX005":
                if (!diagnostics.Logger.IsEnabled(LogLevel.Information))
                    return;

                LogSecondaryIndexCandidateRejected(diagnostics.Logger, diagnostic.Message, null);
                return;

            case "DYNAMO_IDX006":
                if (!diagnostics.Logger.IsEnabled(LogLevel.Information))
                    return;

                LogExplicitIndexSelectionDisabled(diagnostics.Logger, diagnostic.Message, null);
                return;
        }
    }
}
