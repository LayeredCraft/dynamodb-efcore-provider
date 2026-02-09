using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Diagnostics;

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
        RowLimitingQueryWithoutPageSize = CoreEventId.ProviderBaseId + 103,
    }

    private static readonly string CommandPrefix = DbLoggerCategory.Database.Command.Name + ".";

    /// <summary>
    /// A PartiQL query is going to be executed.
    /// </summary>
    /// <remarks>
    /// This event is in the <see cref="DbLoggerCategory.Database.Command" /> category.
    /// </remarks>
    public static readonly EventId ExecutingPartiQlQuery = new(
        (int)Id.ExecutingPartiQlQuery,
        CommandPrefix + Id.ExecutingPartiQlQuery);

    /// <summary>
    /// An ExecuteStatement request is going to be sent.
    /// </summary>
    /// <remarks>
    /// This event is in the <see cref="DbLoggerCategory.Database.Command" /> category.
    /// </remarks>
    public static readonly EventId ExecutingExecuteStatement = new(
        (int)Id.ExecutingExecuteStatement,
        CommandPrefix + Id.ExecutingExecuteStatement);

    /// <summary>
    /// An ExecuteStatement request has completed.
    /// </summary>
    /// <remarks>
    /// This event is in the <see cref="DbLoggerCategory.Database.Command" /> category.
    /// </remarks>
    public static readonly EventId ExecutedExecuteStatement = new(
        (int)Id.ExecutedExecuteStatement,
        CommandPrefix + Id.ExecutedExecuteStatement);

    /// <summary>A row-limiting query is executing without a configured page size.</summary>
    /// <remarks>This event is in the <see cref="DbLoggerCategory.Database.Command" /> category.</remarks>
    public static readonly EventId RowLimitingQueryWithoutPageSize = new(
        (int)Id.RowLimitingQueryWithoutPageSize,
        CommandPrefix + Id.RowLimitingQueryWithoutPageSize);
}
