using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.Diagnostics.Internal;

/// <summary>Defines cached DynamoDB provider logging metadata.</summary>
public class DynamoLoggingDefinition : LoggingDefinitions
{
    /// <summary>Cached event definition for executing PartiQL query logs.</summary>
    public EventDefinition<string, string, string>? LogExecutingPartiQlQuery;

    /// <summary>Cached event definition for executing ExecuteStatement logs.</summary>
    public EventDefinition<int?, bool, bool>? LogExecutingExecuteStatement;

    /// <summary>Cached event definition for executed ExecuteStatement logs.</summary>
    public EventDefinition<int, bool>? LogExecutedExecuteStatement;

    /// <summary>Cached event definition for executing PartiQL write logs.</summary>
    public EventDefinition<string, string, string>? LogExecutingPartiQlWrite;

    /// <summary>Cached event definition for index/scan diagnostic logs.</summary>
    public EventDefinition<string>? LogNoCompatibleSecondaryIndexFound;

    /// <summary>Cached event definition for index/scan diagnostic logs.</summary>
    public EventDefinition<string>? LogMultipleCompatibleSecondaryIndexesFound;

    /// <summary>Cached event definition for index/scan diagnostic logs.</summary>
    public EventDefinition<string>? LogSecondaryIndexSelected;

    /// <summary>Cached event definition for index/scan diagnostic logs.</summary>
    public EventDefinition<string>? LogExplicitIndexSelected;

    /// <summary>Cached event definition for index/scan diagnostic logs.</summary>
    public EventDefinition<string>? LogSecondaryIndexCandidateRejected;

    /// <summary>Cached event definition for index/scan diagnostic logs.</summary>
    public EventDefinition<string>? LogExplicitIndexSelectionDisabled;

    /// <summary>Cached event definition for scan-like query diagnostic logs.</summary>
    public EventDefinition<string>? LogScanLikeQueryDetected;
}
