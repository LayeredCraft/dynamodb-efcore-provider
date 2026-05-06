using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.Diagnostics;

/// <summary>DiagnosticSource payload for DynamoDB PartiQL command text events.</summary>
public class DynamoPartiQlCommandEventData(
    EventDefinitionBase eventDefinition,
    Func<EventDefinitionBase, EventData, string> messageGenerator,
    string tableName,
    string commandText) : EventData(eventDefinition, messageGenerator)
{
    /// <summary>Gets target DynamoDB table name.</summary>
    public virtual string TableName { get; } = tableName;

    /// <summary>Gets PartiQL command text.</summary>
    public virtual string CommandText { get; } = commandText;
}

/// <summary>DiagnosticSource payload for DynamoDB ExecuteStatement request events.</summary>
public class DynamoExecuteStatementEventData(
    EventDefinitionBase eventDefinition,
    Func<EventDefinitionBase, EventData, string> messageGenerator,
    int? limit,
    bool nextTokenPresent,
    bool seedNextTokenPresent,
    Guid commandId = default) : EventData(eventDefinition, messageGenerator)
{
    /// <summary>Gets request limit.</summary>
    public virtual int? Limit { get; } = limit;

    /// <summary>Gets whether continuation token is present.</summary>
    public virtual bool NextTokenPresent { get; } = nextTokenPresent;

    /// <summary>Gets whether seed continuation token is present.</summary>
    public virtual bool SeedNextTokenPresent { get; } = seedNextTokenPresent;

    /// <summary>Gets provider command id for correlating request diagnostics.</summary>
    public virtual Guid CommandId { get; } = commandId;
}

/// <summary>DiagnosticSource payload for completed DynamoDB ExecuteStatement request events.</summary>
public class DynamoExecuteStatementExecutedEventData(
    EventDefinitionBase eventDefinition,
    Func<EventDefinitionBase, EventData, string> messageGenerator,
    int itemsCount,
    bool nextTokenPresent,
    TimeSpan elapsed = default,
    Guid commandId = default,
    string? requestId = null,
    int? limit = null,
    bool seedNextTokenPresent = false,
    ConsumedCapacity? consumedCapacity = null) : EventData(eventDefinition, messageGenerator)
{
    /// <summary>Gets returned item count.</summary>
    public virtual int ItemsCount { get; } = itemsCount;

    /// <summary>Gets whether continuation token is present.</summary>
    public virtual bool NextTokenPresent { get; } = nextTokenPresent;

    /// <summary>Gets request duration.</summary>
    public virtual TimeSpan Elapsed { get; } = elapsed;

    /// <summary>Gets provider command id for correlating request diagnostics.</summary>
    public virtual Guid CommandId { get; } = commandId;

    /// <summary>Gets AWS request id when available.</summary>
    public virtual string? RequestId { get; } = requestId;

    /// <summary>Gets request limit.</summary>
    public virtual int? Limit { get; } = limit;

    /// <summary>Gets whether seed continuation token was present.</summary>
    public virtual bool SeedNextTokenPresent { get; } = seedNextTokenPresent;

    /// <summary>Gets consumed capacity returned by DynamoDB, when requested.</summary>
    public virtual ConsumedCapacity? ConsumedCapacity { get; } = consumedCapacity;
}

/// <summary>DiagnosticSource payload for failed DynamoDB ExecuteStatement request events.</summary>
public class DynamoExecuteStatementFailedEventData(
    EventDefinitionBase eventDefinition,
    Func<EventDefinitionBase, EventData, string> messageGenerator,
    Exception exception,
    TimeSpan elapsed,
    Guid commandId,
    string? requestId,
    int? limit,
    bool nextTokenPresent,
    bool seedNextTokenPresent) : EventData(eventDefinition, messageGenerator)
{
    /// <summary>Gets exception that caused request failure.</summary>
    public virtual Exception Exception { get; } = exception;

    /// <summary>Gets request duration.</summary>
    public virtual TimeSpan Elapsed { get; } = elapsed;

    /// <summary>Gets provider command id for correlating request diagnostics.</summary>
    public virtual Guid CommandId { get; } = commandId;

    /// <summary>Gets AWS request id when available.</summary>
    public virtual string? RequestId { get; } = requestId;

    /// <summary>Gets request limit.</summary>
    public virtual int? Limit { get; } = limit;

    /// <summary>Gets whether continuation token was present.</summary>
    public virtual bool NextTokenPresent { get; } = nextTokenPresent;

    /// <summary>Gets whether seed continuation token was present.</summary>
    public virtual bool SeedNextTokenPresent { get; } = seedNextTokenPresent;
}

/// <summary>DiagnosticSource payload for DynamoDB query diagnostics.</summary>
public class DynamoQueryDiagnosticEventData(
    EventDefinitionBase eventDefinition,
    Func<EventDefinitionBase, EventData, string> messageGenerator,
    string message) : EventData(eventDefinition, messageGenerator)
{
    /// <summary>Gets diagnostic message.</summary>
    public virtual string Message { get; } = message;
}
