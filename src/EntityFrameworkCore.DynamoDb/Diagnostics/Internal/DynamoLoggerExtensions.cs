using EntityFrameworkCore.DynamoDb.Query.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.Diagnostics.Internal;

/// <summary>Represents the DynamoLoggerExtensions type.</summary>
public static class DynamoLoggerExtensions
{
    private static readonly Action<ILogger, string, string, string, Exception?>
        LogExecutingPartiQlQuery = LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            DynamoEventId.ExecutingPartiQlQuery,
            "Executing DynamoDB PartiQL query for table '{tableName}'{newLine}{commandText}");

    private static readonly Action<ILogger, int?, bool, bool, Exception?>
        LogExecutingExecuteStatement = LoggerMessage.Define<int?, bool, bool>(
            LogLevel.Information,
            DynamoEventId.ExecutingExecuteStatement,
            "Executing DynamoDB ExecuteStatement request (limit: {limit}, nextTokenPresent: {nextTokenPresent}, seedNextTokenPresent: {seedNextTokenPresent})");

    private static readonly Action<ILogger, int, bool, Exception?> LogExecutedExecuteStatement =
        LoggerMessage.Define<int, bool>(
            LogLevel.Information,
            DynamoEventId.ExecutedExecuteStatement,
            "Executed DynamoDB ExecuteStatement request (itemsCount: {itemsCount}, nextTokenPresent: {nextTokenPresent})");

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

    private static readonly Action<ILogger, string, string, string, Exception?>
        LogExecutingPartiQlWrite = LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            DynamoEventId.ExecutingPartiQlWrite,
            "Executing DynamoDB PartiQL write for table '{tableName}'{newLine}{commandText}");

    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    public static void ExecutingExecuteStatement(
        this IDiagnosticsLogger<DbLoggerCategory.Database.Command> diagnostics,
        int? limit,
        bool nextTokenPresent,
        bool seedNextTokenPresent = false)
    {
        if (!diagnostics.Logger.IsEnabled(LogLevel.Information))
            return;

        LogExecutingExecuteStatement(
            diagnostics.Logger,
            limit,
            nextTokenPresent,
            seedNextTokenPresent,
            null);
    }

    /// <summary>Provides functionality for this member.</summary>
    public static void ExecutedExecuteStatement(
        this IDiagnosticsLogger<DbLoggerCategory.Database.Command> diagnostics,
        int itemsCount,
        bool nextTokenPresent)
    {
        if (!diagnostics.Logger.IsEnabled(LogLevel.Information))
            return;

        LogExecutedExecuteStatement(diagnostics.Logger, itemsCount, nextTokenPresent, null);
    }

    /// <summary>
    ///     Logs that a PartiQL write statement (INSERT, UPDATE, or DELETE) is about to be executed.
    /// </summary>
    /// <param name="diagnostics">The command diagnostics logger.</param>
    /// <param name="tableName">The target DynamoDB table name.</param>
    /// <param name="commandText">The PartiQL statement text.</param>
    public static void ExecutingPartiQlWrite(
        this IDiagnosticsLogger<DbLoggerCategory.Database.Command> diagnostics,
        string tableName,
        string commandText)
    {
        if (!diagnostics.Logger.IsEnabled(LogLevel.Information))
            return;

        LogExecutingPartiQlWrite(
            diagnostics.Logger,
            tableName,
            Environment.NewLine,
            commandText,
            null);
    }

    /// <summary>Logs a structured query-compilation diagnostic from automatic index selection.</summary>
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
