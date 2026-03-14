using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Represents the DynamoQueryContext type.</summary>
public class DynamoQueryContext(
    QueryContextDependencies dependencies,
    IDynamoClientWrapper client,
    IDiagnosticsLogger<DbLoggerCategory.Database.Command> commandLogger) : QueryContext(
    dependencies)
{
    /// <summary>Provides functionality for this member.</summary>
    public virtual IDynamoClientWrapper Client { get; } = client;

    /// <summary>Provides functionality for this member.</summary>
    public virtual IDiagnosticsLogger<DbLoggerCategory.Database.Command> CommandDiagnosticsLogger
    {
        get;
    } = commandLogger;
}
