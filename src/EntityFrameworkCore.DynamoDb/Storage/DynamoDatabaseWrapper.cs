using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>
/// Translates EF Core <see cref="SaveChanges"/> calls into DynamoDB write operations.
/// </summary>
public class DynamoDatabaseWrapper(
    DatabaseDependencies dependencies,
    IDbContextOptions dbContextOptions,
    ICurrentDbContext currentDbContext,
    DynamoTransactionRuntimeOptions transactionRuntimeOptions,
    IDynamoClientWrapper clientWrapper,
    IDiagnosticsLogger<DbLoggerCategory.Database.Command> commandLogger,
    DynamoEntityItemSerializerSource serializerSource) : Database(dependencies)
{
    private readonly DynamoSaveChangesPlanner _saveChangesPlanner = new(
        serializerSource,
        new DynamoPartiqlStatementFactory(serializerSource));

    private readonly DynamoWriteExecutor _writeExecutor = new(
        dbContextOptions,
        currentDbContext,
        transactionRuntimeOptions,
        clientWrapper,
        commandLogger,
        new DynamoWriteExceptionMapper(),
        new DynamoTransactionTargetIdentityFactory(serializerSource));

    private readonly bool _saveEventsHooked = HookSaveEvents(
        currentDbContext.Context,
        transactionRuntimeOptions);

    /// <summary>Not supported — DynamoDB only exposes an async API.</summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override int SaveChanges(IList<IUpdateEntry> entries)
        => throw new NotSupportedException(
            "DynamoDB does not support synchronous SaveChanges. Use SaveChangesAsync instead.");

    /// <summary>
    /// Persists Added/Modified/Deleted changes to DynamoDB.
    /// </summary>
    public override async Task<int> SaveChangesAsync(
        IList<IUpdateEntry> entries,
        CancellationToken cancellationToken = default)
    {
        _ = _saveEventsHooked;

        try
        {
            var plan = _saveChangesPlanner.Plan(entries);
            if (plan.Operations.Count == 0)
                return 0;

            await _writeExecutor.ExecuteAsync(plan, cancellationToken).ConfigureAwait(false);
            return plan.Operations.Count;
        }
        finally
        {
            transactionRuntimeOptions.AcceptAllChangesOnSuccess = null;
        }
    }

    /// <summary>Hooks SaveChanges events to capture per-call <c>acceptAllChangesOnSuccess</c> mode.</summary>
    private static bool HookSaveEvents(
        DbContext context,
        DynamoTransactionRuntimeOptions transactionRuntimeOptions)
    {
        context.SavingChanges += (_, e) => transactionRuntimeOptions.AcceptAllChangesOnSuccess =
            e.AcceptAllChangesOnSuccess;

        return true;
    }
}
