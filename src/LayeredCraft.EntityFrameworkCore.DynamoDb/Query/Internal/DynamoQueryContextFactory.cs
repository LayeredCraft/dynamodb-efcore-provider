using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public class DynamoQueryContextFactory(
    QueryContextDependencies dependencies,
    IDynamoClientWrapper client,
    IDiagnosticsLogger<DbLoggerCategory.Database.Command> commandLogger) : IQueryContextFactory
{
    public QueryContext Create() => new DynamoQueryContext(dependencies, client, commandLogger);
}
