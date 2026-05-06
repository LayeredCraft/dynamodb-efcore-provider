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
    bool seedNextTokenPresent) : EventData(eventDefinition, messageGenerator)
{
    /// <summary>Gets request limit.</summary>
    public virtual int? Limit { get; } = limit;

    /// <summary>Gets whether continuation token is present.</summary>
    public virtual bool NextTokenPresent { get; } = nextTokenPresent;

    /// <summary>Gets whether seed continuation token is present.</summary>
    public virtual bool SeedNextTokenPresent { get; } = seedNextTokenPresent;
}

/// <summary>DiagnosticSource payload for completed DynamoDB ExecuteStatement request events.</summary>
public class DynamoExecuteStatementExecutedEventData(
    EventDefinitionBase eventDefinition,
    Func<EventDefinitionBase, EventData, string> messageGenerator,
    int itemsCount,
    bool nextTokenPresent) : EventData(eventDefinition, messageGenerator)
{
    /// <summary>Gets returned item count.</summary>
    public virtual int ItemsCount { get; } = itemsCount;

    /// <summary>Gets whether continuation token is present.</summary>
    public virtual bool NextTokenPresent { get; } = nextTokenPresent;
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
