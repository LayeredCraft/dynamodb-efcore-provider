using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
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
}
