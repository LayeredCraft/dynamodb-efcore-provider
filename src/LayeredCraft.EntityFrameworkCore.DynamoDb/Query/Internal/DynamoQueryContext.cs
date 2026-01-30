using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public class DynamoQueryContext(
    QueryContextDependencies dependencies,
    IDynamoClientWrapper client,
    IDiagnosticsLogger<DbLoggerCategory.Database.Command> commandLogger) : QueryContext(
    dependencies)
{
    public virtual IDynamoClientWrapper Client { get; } = client;

    public virtual IDiagnosticsLogger<DbLoggerCategory.Database.Command> CommandDiagnosticsLogger
    {
        get;
    } = commandLogger;
}
