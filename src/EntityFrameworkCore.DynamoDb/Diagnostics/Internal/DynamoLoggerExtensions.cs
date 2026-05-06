using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Query.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.Diagnostics.Internal;

/// <summary>Provides DynamoDB provider logging extensions.</summary>
public static class DynamoLoggerExtensions
{
    private static readonly Func<LogLevel, Action<ILogger, string, string, string, Exception?>>
        LogExecutingPartiQlQuery = level => LoggerMessage.Define<string, string, string>(
            level,
            DynamoEventId.ExecutingPartiQlQuery,
            "Executing DynamoDB PartiQL query for table '{tableName}'{newLine}{commandText}");

    private static readonly Func<LogLevel, Action<ILogger, int?, bool, bool, Exception?>>
        LogExecutingExecuteStatement = level => LoggerMessage.Define<int?, bool, bool>(
            level,
            DynamoEventId.ExecutingExecuteStatement,
            "Executing DynamoDB ExecuteStatement request (limit: {limit}, nextTokenPresent: {nextTokenPresent}, seedNextTokenPresent: {seedNextTokenPresent})");

    private static readonly Func<LogLevel, Action<ILogger, int, bool, Exception?>>
        LogExecutedExecuteStatement = level => LoggerMessage.Define<int, bool>(
            level,
            DynamoEventId.ExecutedExecuteStatement,
            "Executed DynamoDB ExecuteStatement request (itemsCount: {itemsCount}, nextTokenPresent: {nextTokenPresent})");

    private static readonly Func<LogLevel, Action<ILogger, string?, double, Exception?>>
        LogExecuteStatementFailed = level => LoggerMessage.Define<string?, double>(
            level,
            DynamoEventId.ExecuteStatementFailed,
            "Failed executing DynamoDB ExecuteStatement request (requestId: {requestId}, elapsed: {elapsedMilliseconds}ms)");

    private static readonly Func<LogLevel, Action<ILogger, string, string, string, Exception?>>
        LogExecutingPartiQlWrite = level => LoggerMessage.Define<string, string, string>(
            level,
            DynamoEventId.ExecutingPartiQlWrite,
            "Executing DynamoDB PartiQL write for table '{tableName}'{newLine}{commandText}");

    private static readonly Func<LogLevel, Action<ILogger, string, Exception?>> LogMessage = level
        => LoggerMessage.Define<string>(level, default, "{message}");

    /// <summary>Logs that a PartiQL query is about to be executed.</summary>
    public static void ExecutingPartiQlQuery(
        this IDiagnosticsLogger<DbLoggerCategory.Database.Command> diagnostics,
        string tableName,
        string commandText)
    {
        var definition = Definition(
            diagnostics,
            d => d.LogExecutingPartiQlQuery,
            (d, v) => d.LogExecutingPartiQlQuery = v,
            DynamoEventId.ExecutingPartiQlQuery,
            LogLevel.Information,
            LogExecutingPartiQlQuery);

        if (diagnostics.ShouldLog(definition))
            definition.Log(diagnostics, tableName, Environment.NewLine, commandText);
        if (diagnostics.NeedsEventData(definition, out var ds, out var sl))
            diagnostics.DispatchEventData(
                definition,
                new DynamoPartiQlCommandEventData(
                    definition,
                    PartiQlMessage,
                    tableName,
                    commandText),
                ds,
                sl);
    }

    /// <summary>Logs that an ExecuteStatement request is about to be sent.</summary>
    public static void ExecutingExecuteStatement(
        this IDiagnosticsLogger<DbLoggerCategory.Database.Command> diagnostics,
        int? limit,
        bool nextTokenPresent,
        bool seedNextTokenPresent = false,
        Guid commandId = default)
    {
        var definition = Definition(
            diagnostics,
            d => d.LogExecutingExecuteStatement,
            (d, v) => d.LogExecutingExecuteStatement = v,
            DynamoEventId.ExecutingExecuteStatement,
            LogLevel.Information,
            LogExecutingExecuteStatement);

        if (diagnostics.ShouldLog(definition))
            definition.Log(diagnostics, limit, nextTokenPresent, seedNextTokenPresent);
        if (diagnostics.NeedsEventData(definition, out var ds, out var sl))
            diagnostics.DispatchEventData(
                definition,
                new DynamoExecuteStatementEventData(
                    definition,
                    ExecutingExecuteStatementMessage,
                    limit,
                    nextTokenPresent,
                    seedNextTokenPresent,
                    commandId),
                ds,
                sl);
    }

    /// <summary>Logs that an ExecuteStatement request completed.</summary>
    public static void ExecutedExecuteStatement(
        this IDiagnosticsLogger<DbLoggerCategory.Database.Command> diagnostics,
        int itemsCount,
        bool nextTokenPresent,
        TimeSpan elapsed = default,
        Guid commandId = default,
        string? requestId = null,
        int? limit = null,
        bool seedNextTokenPresent = false,
        ConsumedCapacity? consumedCapacity = null)
    {
        var definition = Definition(
            diagnostics,
            d => d.LogExecutedExecuteStatement,
            (d, v) => d.LogExecutedExecuteStatement = v,
            DynamoEventId.ExecutedExecuteStatement,
            LogLevel.Information,
            LogExecutedExecuteStatement);

        if (diagnostics.ShouldLog(definition))
            definition.Log(diagnostics, itemsCount, nextTokenPresent);
        if (diagnostics.NeedsEventData(definition, out var ds, out var sl))
            diagnostics.DispatchEventData(
                definition,
                new DynamoExecuteStatementExecutedEventData(
                    definition,
                    ExecutedExecuteStatementMessage,
                    itemsCount,
                    nextTokenPresent,
                    elapsed,
                    commandId,
                    requestId,
                    limit,
                    seedNextTokenPresent,
                    consumedCapacity),
                ds,
                sl);
    }

    /// <summary>Logs that an ExecuteStatement request failed.</summary>
    public static void ExecuteStatementFailed(
        this IDiagnosticsLogger<DbLoggerCategory.Database.Command> diagnostics,
        Exception exception,
        TimeSpan elapsed,
        Guid commandId,
        string? requestId,
        int? limit,
        bool nextTokenPresent,
        bool seedNextTokenPresent)
    {
        var definition = Definition(
            diagnostics,
            d => d.LogExecuteStatementFailed,
            (d, v) => d.LogExecuteStatementFailed = v,
            DynamoEventId.ExecuteStatementFailed,
            LogLevel.Error,
            LogExecuteStatementFailed);

        if (diagnostics.ShouldLog(definition))
            definition.Log(diagnostics, requestId, elapsed.TotalMilliseconds);
        if (diagnostics.NeedsEventData(definition, out var ds, out var sl))
            diagnostics.DispatchEventData(
                definition,
                new DynamoExecuteStatementFailedEventData(
                    definition,
                    ExecuteStatementFailedMessage,
                    exception,
                    elapsed,
                    commandId,
                    requestId,
                    limit,
                    nextTokenPresent,
                    seedNextTokenPresent),
                ds,
                sl);
    }

    /// <summary>Logs that a PartiQL write statement is about to be executed.</summary>
    public static void ExecutingPartiQlWrite(
        this IDiagnosticsLogger<DbLoggerCategory.Database.Command> diagnostics,
        string tableName,
        string commandText)
    {
        var definition = Definition(
            diagnostics,
            d => d.LogExecutingPartiQlWrite,
            (d, v) => d.LogExecutingPartiQlWrite = v,
            DynamoEventId.ExecutingPartiQlWrite,
            LogLevel.Information,
            LogExecutingPartiQlWrite);

        if (diagnostics.ShouldLog(definition))
            definition.Log(diagnostics, tableName, Environment.NewLine, commandText);
        if (diagnostics.NeedsEventData(definition, out var ds, out var sl))
            diagnostics.DispatchEventData(
                definition,
                new DynamoPartiQlCommandEventData(
                    definition,
                    PartiQlMessage,
                    tableName,
                    commandText),
                ds,
                sl);
    }

    /// <summary>Logs a structured query-compilation diagnostic from automatic index selection.</summary>
    internal static void IndexSelectionDiagnostic(
        this IDiagnosticsLogger<DbLoggerCategory.Query> diagnostics,
        DynamoQueryDiagnostic diagnostic)
    {
        var (eventId, level, get, set) = diagnostic.Code switch
        {
            "DYNAMO_IDX001" => (DynamoEventId.NoCompatibleSecondaryIndexFound, LogLevel.Warning,
                (Func<DynamoLoggingDefinition, EventDefinition<string>?>)(d
                    => d.LogNoCompatibleSecondaryIndexFound),
                (Action<DynamoLoggingDefinition, EventDefinition<string>>)((d, v)
                    => d.LogNoCompatibleSecondaryIndexFound = v)),
            "DYNAMO_IDX002" => (DynamoEventId.MultipleCompatibleSecondaryIndexesFound,
                LogLevel.Warning, d => d.LogMultipleCompatibleSecondaryIndexesFound,
                (d, v) => d.LogMultipleCompatibleSecondaryIndexesFound = v),
            "DYNAMO_IDX003" => (DynamoEventId.SecondaryIndexSelected, LogLevel.Information,
                d => d.LogSecondaryIndexSelected, (d, v) => d.LogSecondaryIndexSelected = v),
            "DYNAMO_IDX004" => (DynamoEventId.ExplicitIndexSelected, LogLevel.Information,
                d => d.LogExplicitIndexSelected, (d, v) => d.LogExplicitIndexSelected = v),
            "DYNAMO_IDX005" => (DynamoEventId.SecondaryIndexCandidateRejected, LogLevel.Information,
                d => d.LogSecondaryIndexCandidateRejected,
                (d, v) => d.LogSecondaryIndexCandidateRejected = v),
            "DYNAMO_IDX006" => (DynamoEventId.ExplicitIndexSelectionDisabled, LogLevel.Information,
                d => d.LogExplicitIndexSelectionDisabled,
                (d, v) => d.LogExplicitIndexSelectionDisabled = v),
            _ => default
        };
        if (eventId.Id == 0)
            return;
        QueryDiagnostic(diagnostics, diagnostic.Message, eventId, level, get, set);
    }

    /// <summary>Logs that a scan-like read query was detected and allowed to continue.</summary>
    internal static void ScanLikeQueryDetected(
        this IDiagnosticsLogger<DbLoggerCategory.Query> diagnostics,
        string message)
        => QueryDiagnostic(
            diagnostics,
            message,
            DynamoEventId.ScanLikeQueryDetected,
            LogLevel.Warning,
            d => d.LogScanLikeQueryDetected,
            (d, v) => d.LogScanLikeQueryDetected = v);

    private static void QueryDiagnostic(
        IDiagnosticsLogger<DbLoggerCategory.Query> diagnostics,
        string message,
        EventId eventId,
        LogLevel level,
        Func<DynamoLoggingDefinition, EventDefinition<string>?> get,
        Action<DynamoLoggingDefinition, EventDefinition<string>> set)
    {
        var definition = Definition(
            diagnostics,
            get,
            set,
            eventId,
            level,
            l => LoggerMessage.Define<string>(l, eventId, "{message}"));
        if (diagnostics.ShouldLog(definition))
            definition.Log(diagnostics, message);
        if (diagnostics.NeedsEventData(definition, out var ds, out var sl))
            diagnostics.DispatchEventData(
                definition,
                new DynamoQueryDiagnosticEventData(definition, QueryDiagnosticMessage, message),
                ds,
                sl);
    }

    private static EventDefinition<T1, T2, T3> Definition<TLoggerCategory, T1, T2, T3>(
        IDiagnosticsLogger<TLoggerCategory> diagnostics,
        Func<DynamoLoggingDefinition, EventDefinition<T1, T2, T3>?> get,
        Action<DynamoLoggingDefinition, EventDefinition<T1, T2, T3>> set,
        EventId eventId,
        LogLevel level,
        Func<LogLevel, Action<ILogger, T1, T2, T3, Exception?>> factory)
        where TLoggerCategory : LoggerCategory<TLoggerCategory>, new()
        => Get(
            diagnostics,
            get,
            set,
            _ => new EventDefinition<T1, T2, T3>(
                diagnostics.Options,
                eventId,
                level,
                eventId.Name!,
                factory));

    private static EventDefinition<T1, T2> Definition<TLoggerCategory, T1, T2>(
        IDiagnosticsLogger<TLoggerCategory> diagnostics,
        Func<DynamoLoggingDefinition, EventDefinition<T1, T2>?> get,
        Action<DynamoLoggingDefinition, EventDefinition<T1, T2>> set,
        EventId eventId,
        LogLevel level,
        Func<LogLevel, Action<ILogger, T1, T2, Exception?>> factory)
        where TLoggerCategory : LoggerCategory<TLoggerCategory>, new()
        => Get(
            diagnostics,
            get,
            set,
            _ => new EventDefinition<T1, T2>(
                diagnostics.Options,
                eventId,
                level,
                eventId.Name!,
                factory));

    private static EventDefinition<T> Definition<TLoggerCategory, T>(
        IDiagnosticsLogger<TLoggerCategory> diagnostics,
        Func<DynamoLoggingDefinition, EventDefinition<T>?> get,
        Action<DynamoLoggingDefinition, EventDefinition<T>> set,
        EventId eventId,
        LogLevel level,
        Func<LogLevel, Action<ILogger, T, Exception?>> factory)
        where TLoggerCategory : LoggerCategory<TLoggerCategory>, new()
        => Get(
            diagnostics,
            get,
            set,
            _ => new EventDefinition<T>(
                diagnostics.Options,
                eventId,
                level,
                eventId.Name!,
                factory));

    private static TDefinition Get<TLoggerCategory, TDefinition>(
        IDiagnosticsLogger<TLoggerCategory> diagnostics,
        Func<DynamoLoggingDefinition, TDefinition?> get,
        Action<DynamoLoggingDefinition, TDefinition> set,
        Func<DynamoLoggingDefinition, TDefinition> create)
        where TLoggerCategory : LoggerCategory<TLoggerCategory>, new() where TDefinition : class
    {
        var definitions = (DynamoLoggingDefinition)diagnostics.Definitions;
        var definition = get(definitions);
        if (definition is not null)
            return definition;
        definition = create(definitions);
        set(definitions, definition);
        return definition;
    }

    private static string PartiQlMessage(EventDefinitionBase definition, EventData payload)
    {
        var d = (EventDefinition<string, string, string>)definition;
        var p = (DynamoPartiQlCommandEventData)payload;
        return d.GenerateMessage(p.TableName, Environment.NewLine, p.CommandText);
    }

    private static string ExecutingExecuteStatementMessage(
        EventDefinitionBase definition,
        EventData payload)
    {
        var d = (EventDefinition<int?, bool, bool>)definition;
        var p = (DynamoExecuteStatementEventData)payload;
        return d.GenerateMessage(p.Limit, p.NextTokenPresent, p.SeedNextTokenPresent);
    }

    private static string ExecutedExecuteStatementMessage(
        EventDefinitionBase definition,
        EventData payload)
    {
        var d = (EventDefinition<int, bool>)definition;
        var p = (DynamoExecuteStatementExecutedEventData)payload;
        return d.GenerateMessage(p.ItemsCount, p.NextTokenPresent);
    }

    private static string ExecuteStatementFailedMessage(
        EventDefinitionBase definition,
        EventData payload)
    {
        var d = (EventDefinition<string?, double>)definition;
        var p = (DynamoExecuteStatementFailedEventData)payload;
        return d.GenerateMessage(p.RequestId, p.Elapsed.TotalMilliseconds);
    }

    private static string QueryDiagnosticMessage(EventDefinitionBase definition, EventData payload)
        => ((EventDefinition<string>)definition).GenerateMessage(
            ((DynamoQueryDiagnosticEventData)payload).Message);
}
