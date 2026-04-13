using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.Diagnostics;

/// <summary>
/// Event IDs for DynamoDB provider events.
/// </summary>
public static class DynamoEventId
{
    // Warning: These values must not change between releases.
    private enum Id
    {
        // Command events
        ExecutingPartiQlQuery = CoreEventId.ProviderBaseId + 100,
        ExecutingExecuteStatement = CoreEventId.ProviderBaseId + 101,
        ExecutedExecuteStatement = CoreEventId.ProviderBaseId + 102,
        ExecutingPartiQlWrite = CoreEventId.ProviderBaseId + 110,
        UntrackedOwnedCollectionElement = CoreEventId.ProviderBaseId + 111,

        // Query events
        NoCompatibleSecondaryIndexFound = CoreEventId.ProviderBaseId + 104,
        MultipleCompatibleSecondaryIndexesFound = CoreEventId.ProviderBaseId + 105,
        SecondaryIndexSelected = CoreEventId.ProviderBaseId + 106,
        ExplicitIndexSelected = CoreEventId.ProviderBaseId + 107,
        SecondaryIndexCandidateRejected = CoreEventId.ProviderBaseId + 108,
        ExplicitIndexSelectionDisabled = CoreEventId.ProviderBaseId + 109,
    }

    private static readonly string CommandPrefix = DbLoggerCategory.Database.Command.Name + ".";
    private static readonly string QueryPrefix = DbLoggerCategory.Query.Name + ".";

    /// <summary>
    /// A PartiQL query is going to be executed.
    /// </summary>
    /// <remarks>
    /// This event is in the <c>DbLoggerCategory.Database.Command</c> category.
    /// </remarks>
    public static readonly EventId ExecutingPartiQlQuery = new(
        (int)Id.ExecutingPartiQlQuery,
        CommandPrefix + Id.ExecutingPartiQlQuery);

    /// <summary>
    /// An ExecuteStatement request is going to be sent.
    /// </summary>
    /// <remarks>
    /// This event is in the <c>DbLoggerCategory.Database.Command</c> category.
    /// </remarks>
    public static readonly EventId ExecutingExecuteStatement = new(
        (int)Id.ExecutingExecuteStatement,
        CommandPrefix + Id.ExecutingExecuteStatement);

    /// <summary>
    /// An ExecuteStatement request has completed.
    /// </summary>
    /// <remarks>
    /// This event is in the <c>DbLoggerCategory.Database.Command</c> category.
    /// </remarks>
    public static readonly EventId ExecutedExecuteStatement = new(
        (int)Id.ExecutedExecuteStatement,
        CommandPrefix + Id.ExecutedExecuteStatement);

    /// <summary>
    /// A PartiQL write statement (INSERT, UPDATE, or DELETE) is going to be executed.
    /// </summary>
    /// <remarks>
    /// This event is in the <c>DbLoggerCategory.Database.Command</c> category.
    /// </remarks>
    public static readonly EventId ExecutingPartiQlWrite = new(
        (int)Id.ExecutingPartiQlWrite,
        CommandPrefix + Id.ExecutingPartiQlWrite);

    /// <summary>
    /// An owned collection element was skipped during SaveChanges because it is not tracked by
    /// the EF Core change tracker.
    /// </summary>
    /// <remarks>This event is in the <c>DbLoggerCategory.Database.Command</c> category.</remarks>
    public static readonly EventId UntrackedOwnedCollectionElement = new(
        (int)Id.UntrackedOwnedCollectionElement,
        CommandPrefix + Id.UntrackedOwnedCollectionElement);

    /// <summary>No compatible secondary index was found for automatic selection.</summary>
    /// <remarks>This event is in the <c>DbLoggerCategory.Query</c> category.</remarks>
    public static readonly EventId NoCompatibleSecondaryIndexFound = new(
        (int)Id.NoCompatibleSecondaryIndexFound,
        QueryPrefix + Id.NoCompatibleSecondaryIndexFound);

    /// <summary>Multiple compatible secondary indexes tied during automatic selection.</summary>
    /// <remarks>This event is in the <c>DbLoggerCategory.Query</c> category.</remarks>
    public static readonly EventId MultipleCompatibleSecondaryIndexesFound = new(
        (int)Id.MultipleCompatibleSecondaryIndexesFound,
        QueryPrefix + Id.MultipleCompatibleSecondaryIndexesFound);

    /// <summary>A secondary index was selected, or would be selected, for the query.</summary>
    /// <remarks>This event is in the <c>DbLoggerCategory.Query</c> category.</remarks>
    public static readonly EventId SecondaryIndexSelected = new(
        (int)Id.SecondaryIndexSelected,
        QueryPrefix + Id.SecondaryIndexSelected);

    /// <summary>A secondary index was explicitly selected via <c>.WithIndex()</c>.</summary>
    /// <remarks>This event is in the <c>DbLoggerCategory.Query</c> category.</remarks>
    public static readonly EventId ExplicitIndexSelected = new(
        (int)Id.ExplicitIndexSelected,
        QueryPrefix + Id.ExplicitIndexSelected);

    /// <summary>A secondary index candidate was rejected during automatic index selection.</summary>
    /// <remarks>This event is in the <c>DbLoggerCategory.Query</c> category.</remarks>
    public static readonly EventId SecondaryIndexCandidateRejected = new(
        (int)Id.SecondaryIndexCandidateRejected,
        QueryPrefix + Id.SecondaryIndexCandidateRejected);

    /// <summary>Index selection was suppressed by <c>.WithoutIndex()</c>.</summary>
    /// <remarks>This event is in the <c>DbLoggerCategory.Query</c> category.</remarks>
    public static readonly EventId ExplicitIndexSelectionDisabled = new(
        (int)Id.ExplicitIndexSelectionDisabled,
        QueryPrefix + Id.ExplicitIndexSelectionDisabled);
}
