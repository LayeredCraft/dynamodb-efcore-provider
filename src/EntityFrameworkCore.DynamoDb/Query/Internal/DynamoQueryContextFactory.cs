using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Represents the DynamoQueryContextFactory type.</summary>
public class DynamoQueryContextFactory(
    QueryContextDependencies dependencies,
    IDynamoClientWrapper client,
    IDiagnosticsLogger<DbLoggerCategory.Database.Command> commandLogger,
    IDiagnosticsLogger<DbLoggerCategory.Query> queryLogger) : IQueryContextFactory
{
    /// <summary>Provides functionality for this member.</summary>
    public QueryContext Create()
        => new DynamoQueryContext(dependencies, client, commandLogger, queryLogger);
}
