using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Represents the DynamoQueryContext type.</summary>
public class DynamoQueryContext(
    QueryContextDependencies dependencies,
    IDynamoClientWrapper client,
    IDiagnosticsLogger<DbLoggerCategory.Database.Command> commandLogger,
    IDiagnosticsLogger<DbLoggerCategory.Query> queryLogger) : QueryContext(dependencies)
{
    /// <summary>Provides functionality for this member.</summary>
    public virtual IDynamoClientWrapper Client { get; } = client;

    /// <summary>Provides functionality for this member.</summary>
    public virtual IDiagnosticsLogger<DbLoggerCategory.Database.Command> CommandDiagnosticsLogger
    {
        get;
    } = commandLogger;

    /// <summary>Provides functionality for this member.</summary>
    public virtual IDiagnosticsLogger<DbLoggerCategory.Query> QueryDiagnosticsLogger { get; } =
        queryLogger;

    /// <summary>
    ///     The <see cref="ExecuteStatementResponse" /> from the most recently fetched DynamoDB page.
    ///     Set by <see cref="DynamoClientWrapper" /> before items from each page are yielded, and read by
    ///     the shaper expression during materialization to populate the per-entity shadow property.
    /// </summary>
    public ExecuteStatementResponse? CurrentPageResponse { get; set; }
}
