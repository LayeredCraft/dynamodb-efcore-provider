using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using EntityFrameworkCore.DynamoDb.Storage.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>Provides async database lifecycle operations for mapped DynamoDB tables.</summary>
internal sealed class DynamoDatabaseCreator(
    ICurrentDbContext currentDbContext,
    IDynamoClientWrapper clientWrapper,
    IDesignTimeModel designTimeModel,
    IDatabase database,
    IUpdateAdapterFactory updateAdapterFactory,
    IDbContextOptions contextOptions,
    IExecutionStrategy executionStrategy) : IDatabaseCreator
{
    private const string AsyncLifecycleOnly =
        "The DynamoDB database provider only supports async database lifecycle operations. Use EnsureCreatedAsync, EnsureDeletedAsync, or CanConnectAsync.";

    // Caps concurrent delete calls to avoid hammering DynamoDB metadata APIs.
    private const int MaxConcurrentTableOperations = 10;

    /// <summary>Ensures the database is deleted.</summary>
    /// <returns>Never returns because synchronous lifecycle operations are unsupported.</returns>
    /// <exception cref="NotSupportedException">Always thrown because only async lifecycle operations are supported.</exception>
    public bool EnsureDeleted() => throw new NotSupportedException(AsyncLifecycleOnly);

    /// <summary>Ensures mapped DynamoDB tables are deleted asynchronously.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true" /> when at least one mapped table existed and was deleted.</returns>
    public async Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default)
    {
        var lifecycleOptions = GetTableLifecycleOptions();
        using var semaphore = new SemaphoreSlim(MaxConcurrentTableOperations);

        var tasks = GetRuntimeTableModel()
            .Tables
            .Values
            .OrderBy(static t => t.TableName, StringComparer.Ordinal)
            .Select(async Task<(bool Deleted, Exception? Error)> (table) =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    try
                    {
                        await clientWrapper
                            .Client
                            .DeleteTableAsync(
                                new DeleteTableRequest { TableName = table.TableName },
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (ResourceNotFoundException)
                    {
                        return (Deleted: false, Error: null);
                    }

                    if (lifecycleOptions.WaitForCompletion)
                        await WaitUntilTableDeletedAsync(
                                table.TableName,
                                lifecycleOptions,
                                cancellationToken)
                            .ConfigureAwait(false);

                    return (Deleted: true, Error: null);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    return (Deleted: false, Error: ex);
                }
                finally
                {
                    semaphore.Release();
                }
            })
            .ToArray();

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var errors = results
            .Where(static r => r.Error is not null)
            .Select(static r => r.Error!)
            .ToList();
        if (errors.Count > 0)
            throw new AggregateException(
                "One or more DynamoDB table deletion operations failed.",
                errors);

        return results.Any(static r => r.Deleted);
    }

    /// <summary>Ensures mapped DynamoDB tables and global secondary indexes are created asynchronously.</summary>
    /// <returns>Never returns because synchronous lifecycle operations are unsupported.</returns>
    /// <exception cref="NotSupportedException">Always thrown because only async lifecycle operations are supported.</exception>
    public bool EnsureCreated() => throw new NotSupportedException(AsyncLifecycleOnly);

    /// <summary>Ensures mapped DynamoDB tables and global secondary indexes are created asynchronously.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true" /> when a table or global secondary index was created.</returns>
    public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        var retrying = false;
        var modelSeedInserted = false;

        return executionStrategy.ExecuteAsync(
            this,
            async (_, creator, ct) =>
            {
                if (retrying)
                    currentDbContext.Context.ChangeTracker.Clear();
                retrying = true;

                var (changed, createdTables) =
                    await creator.EnsureTablesCreatedAsync(ct).ConfigureAwait(false);
                var tableCreated = createdTables.Count > 0;
                if (tableCreated && creator.HasConfiguredSeeding(createdTables))
                    await creator
                        .WaitUntilTablesActiveAsync(createdTables, ct)
                        .ConfigureAwait(false);

                if (tableCreated && !modelSeedInserted)
                {
                    // Model HasData runs only after new table creation. User async seeding runs on
                    // every EnsureCreatedAsync call with the schema-created flag, including
                    // GSI-only updates.
                    await creator.InsertDataAsync(createdTables, ct).ConfigureAwait(false);
                    modelSeedInserted = true;
                }

                await creator.SeedDataAsync(changed, ct).ConfigureAwait(false);

                return changed;
            },
            null,
            cancellationToken);
    }

    /// <summary>Determines whether the database can be connected to.</summary>
    /// <returns>Never returns because synchronous lifecycle operations are unsupported.</returns>
    /// <exception cref="NotSupportedException">Always thrown because only async lifecycle operations are supported.</exception>
    public bool CanConnect() => throw new NotSupportedException(AsyncLifecycleOnly);

    /// <summary>Determines whether DynamoDB can be connected to asynchronously.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true" /> when a low-side-effect list-tables probe succeeds.</returns>
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await clientWrapper
                .Client
                .ListTablesAsync(new ListTablesRequest { Limit = 1 }, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<(bool Changed, HashSet<string> CreatedTables)> EnsureTablesCreatedAsync(
        CancellationToken cancellationToken)
    {
        var runtimeModel = GetRuntimeTableModel();
        var requestsByName =
            DynamoTableDefinitionBuilder
                .BuildCreateTableRequests(runtimeModel)
                .ToDictionary(static request => request.TableName, StringComparer.Ordinal);
        var lifecycleOptions = GetTableLifecycleOptions();

        HashSet<string> createdTables = new(StringComparer.Ordinal);
        var changed = false;
        foreach (var table in runtimeModel.Tables.Values.OrderBy(
            static table => table.TableName,
            StringComparer.Ordinal))
        {
            var (tableChanged, createdTable) = await EnsureTableCreatedAsync(
                    table,
                    requestsByName,
                    lifecycleOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            changed |= tableChanged;
            if (createdTable is not null)
                createdTables.Add(createdTable);
        }

        return (changed, createdTables);
    }

    private async Task<(bool Changed, string? CreatedTableName)> EnsureTableCreatedAsync(
        DynamoTableDescriptor table,
        Dictionary<string, CreateTableRequest> requestsByName,
        DynamoTableLifecycleOptions lifecycleOptions,
        CancellationToken cancellationToken)
    {
        var request = requestsByName[table.TableName];
        var hasSecondaryIndexes = HasSecondaryIndexes(request);
        var changed = false;
        string? createdTableName = null;

        var tableIsNew = false;
        TableDescription? existing = null;
        try
        {
            existing =
                (await clientWrapper
                    .Client
                    .DescribeTableAsync(
                        new DescribeTableRequest { TableName = table.TableName },
                        cancellationToken)
                    .ConfigureAwait(false)).Table;
        }
        catch (ResourceNotFoundException)
        {
            tableIsNew = true;
            try
            {
                existing = (await clientWrapper
                    .Client
                    .CreateTableAsync(request, cancellationToken)
                    .ConfigureAwait(false)).TableDescription;
                changed = true;
                createdTableName = table.TableName;
            }
            catch (ResourceInUseException)
            {
                // Creation race: fall through to describe/wait.
            }

            // DynamoDB rejects concurrent table creation when secondary indexes are involved.
            // Always wait for indexed table creates, even when optional lifecycle waiting is
            // disabled.
            if (lifecycleOptions.WaitForCompletion || hasSecondaryIndexes)
                existing = await WaitUntilTableActiveAsync(
                        table.TableName,
                        lifecycleOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
            else if (existing is null)
                existing =
                    (await clientWrapper
                        .Client
                        .DescribeTableAsync(
                            new DescribeTableRequest { TableName = table.TableName },
                            cancellationToken)
                        .ConfigureAwait(false)).Table;
        }

        if (!tableIsNew && lifecycleOptions.WaitForCompletion && !IsFullyActive(existing))
            existing = await WaitUntilTableActiveAsync(
                    table.TableName,
                    lifecycleOptions,
                    cancellationToken)
                .ConfigureAwait(false);

        // When WaitForCompletion is false and the table already existed, `existing` is
        // the snapshot from the initial DescribeTable and may not reflect concurrent GSI
        // additions by other processes. A duplicate UpdateTable for an already-present
        // GSI will fail with ResourceInUseException.
        var updates =
            DynamoTableDefinitionBuilder.BuildMissingGlobalSecondaryIndexUpdates(table, existing);
        for (var i = 0; i < updates.Count; i++)
        {
            await clientWrapper
                .Client
                .UpdateTableAsync(
                    new UpdateTableRequest
                    {
                        TableName = table.TableName,
                        AttributeDefinitions = updates[i].AttributeDefinitions.ToList(),
                        GlobalSecondaryIndexUpdates = [updates[i].Update],
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            changed = true;
            // Always wait after GSI additions: DynamoDB rejects overlapping secondary-index
            // lifecycle operations while table or index status is still changing.
            await WaitUntilTableActiveAsync(table.TableName, lifecycleOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        return (changed, createdTableName);
    }

    private async Task WaitUntilTablesActiveAsync(
        IEnumerable<string> tableNames,
        CancellationToken cancellationToken)
    {
        var lifecycleOptions = GetTableLifecycleOptions();
        foreach (var tableName in tableNames.Order(StringComparer.Ordinal))
            await WaitUntilTableActiveAsync(tableName, lifecycleOptions, cancellationToken)
                .ConfigureAwait(false);
    }

    private bool HasConfiguredSeeding(IReadOnlySet<string> createdTables)
        => contextOptions.FindExtension<CoreOptionsExtension>()?.AsyncSeeder is not null
            || designTimeModel
                .Model
                .GetEntityTypes()
                .Any(entityType
                    => createdTables.Contains(
                        entityType[DynamoAnnotationNames.TableName] as string
                        ?? entityType.ClrType.Name)
                    && entityType.GetSeedData().Any());

    private Task InsertDataAsync(
        IReadOnlySet<string> createdTables,
        CancellationToken cancellationToken)
    {
        var updateAdapter = updateAdapterFactory.CreateStandalone();
        foreach (var entityType in designTimeModel.Model.GetEntityTypes())
        {
            var tableName = entityType[DynamoAnnotationNames.TableName] as string
                ?? entityType.ClrType.Name;
            if (!createdTables.Contains(tableName))
                continue;

            foreach (var targetSeed in entityType.GetSeedData())
            {
                var runtimeEntityType = updateAdapter.Model.FindEntityType(entityType.Name)!;
                var entry = updateAdapter.CreateEntry(targetSeed, runtimeEntityType);
                entry.EntityState = EntityState.Added;
            }
        }

        return database.SaveChangesAsync(updateAdapter.GetEntriesToSave(), cancellationToken);
    }

    private async Task SeedDataAsync(bool created, CancellationToken cancellationToken)
    {
        var coreOptionsExtension = contextOptions.FindExtension<CoreOptionsExtension>();
        if (coreOptionsExtension?.AsyncSeeder is not null)
            await coreOptionsExtension
                .AsyncSeeder(currentDbContext.Context, created, cancellationToken)
                .ConfigureAwait(false);
        else if (coreOptionsExtension?.Seeder is not null)
            throw new InvalidOperationException(
                "A synchronous seeder has been configured, but the DynamoDB provider only supports async lifecycle operations. Configure UseAsyncSeeding instead.");
    }

    private DynamoRuntimeTableModel GetRuntimeTableModel()
        => currentDbContext.Context.Model.GetDynamoRuntimeTableModel()
            ?? throw new InvalidOperationException(
                "DynamoDB runtime table model is missing from the EF model.");

    private DynamoTableLifecycleOptions GetTableLifecycleOptions()
        => contextOptions.FindExtension<DynamoDbOptionsExtension>()?.TableLifecycleOptions
            ?? new DynamoTableLifecycleOptions();

    private static bool HasSecondaryIndexes(CreateTableRequest request)
        => request.GlobalSecondaryIndexes is { Count: > 0 }
            || request.LocalSecondaryIndexes is { Count: > 0 };

    private static bool IsFullyActive(TableDescription? table)
        => table is not null
            && table.TableStatus == TableStatus.ACTIVE
            && (table.GlobalSecondaryIndexes ?? []).All(static index
                => index.IndexStatus == IndexStatus.ACTIVE);

    private async Task<TableDescription> WaitUntilTableActiveAsync(
        string tableName,
        DynamoTableLifecycleOptions lifecycleOptions,
        CancellationToken cancellationToken)
    {
        var started = TimeProvider.System.GetTimestamp();
        var delay = lifecycleOptions.InitialPollingDelay;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var table =
                    (await clientWrapper
                        .Client
                        .DescribeTableAsync(
                            new DescribeTableRequest { TableName = tableName },
                            cancellationToken)
                        .ConfigureAwait(false)).Table;
                if (IsFullyActive(table))
                    return table;
            }
            catch (ResourceNotFoundException)
            {
                // Creation race: keep polling.
            }

            await DelayLifecyclePollAsync(lifecycleOptions, started, delay, cancellationToken)
                .ConfigureAwait(false);
            delay = NextPollingDelay(
                delay,
                lifecycleOptions.MaxPollingDelay,
                lifecycleOptions.BackoffMultiplier);
        }
    }

    private async Task WaitUntilTableDeletedAsync(
        string tableName,
        DynamoTableLifecycleOptions lifecycleOptions,
        CancellationToken cancellationToken)
    {
        var started = TimeProvider.System.GetTimestamp();
        var delay = lifecycleOptions.InitialPollingDelay;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await clientWrapper
                    .Client
                    .DescribeTableAsync(
                        new DescribeTableRequest { TableName = tableName },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ResourceNotFoundException)
            {
                return;
            }

            await DelayLifecyclePollAsync(lifecycleOptions, started, delay, cancellationToken)
                .ConfigureAwait(false);
            delay = NextPollingDelay(
                delay,
                lifecycleOptions.MaxPollingDelay,
                lifecycleOptions.BackoffMultiplier);
        }
    }

    private static async Task DelayLifecyclePollAsync(
        DynamoTableLifecycleOptions options,
        long started,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        if (options.Timeout is { } timeout)
        {
            var elapsed = TimeProvider.System.GetElapsedTime(started);
            if (elapsed >= timeout)
                throw new TimeoutException(
                    $"Timed out after {timeout} waiting for DynamoDB table lifecycle operation to complete.");

            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks, (timeout - elapsed).Ticks));
        }

        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }

    private static TimeSpan NextPollingDelay(TimeSpan current, TimeSpan maximum, double multiplier)
    {
        // Clamp to maximum.Ticks as a double before casting to avoid long overflow when
        // current.Ticks * multiplier exceeds long.MaxValue.
        var nextTicks = (long)Math.Min(Math.Ceiling(current.Ticks * multiplier), maximum.Ticks);
        if (nextTicks <= current.Ticks)
            nextTicks = current.Ticks + 1;

        return TimeSpan.FromTicks(Math.Min(nextTicks, maximum.Ticks));
    }
}
