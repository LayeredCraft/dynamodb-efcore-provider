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
        var changed = false;
        foreach (var table in GetRuntimeTableModel()
            .Tables
            .Values
            .OrderBy(static table => table.TableName, StringComparer.Ordinal))
        {
            try
            {
                await clientWrapper
                    .Client
                    .DeleteTableAsync(
                        new DeleteTableRequest { TableName = table.TableName },
                        cancellationToken)
                    .ConfigureAwait(false);
                changed = true;
            }
            catch (ResourceNotFoundException)
            {
                continue;
            }

            if (lifecycleOptions.WaitForCompletion)
                await WaitUntilTableDeletedAsync(table.TableName, cancellationToken)
                    .ConfigureAwait(false);
        }

        return changed;
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
                if (tableCreated && !modelSeedInserted)
                {
                    // Model HasData runs only after new table creation. User async seeding runs on
                    // every
                    // EnsureCreatedAsync call with the schema-created flag, including GSI-only
                    // updates.
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
        var changed = false;
        HashSet<string> createdTables = new(StringComparer.Ordinal);

        foreach (var table in runtimeModel.Tables.Values.OrderBy(
            static table => table.TableName,
            StringComparer.Ordinal))
        {
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
                        .CreateTableAsync(requestsByName[table.TableName], cancellationToken)
                        .ConfigureAwait(false)).TableDescription;
                    changed = true;
                    createdTables.Add(table.TableName);
                }
                catch (ResourceInUseException)
                {
                    // Creation race: fall through to describe/wait.
                }

                if (lifecycleOptions.WaitForCompletion)
                    existing =
                        await WaitUntilTableActiveAsync(table.TableName, cancellationToken)
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

            if (!tableIsNew && lifecycleOptions.WaitForCompletion)
                existing = await WaitUntilTableActiveAsync(table.TableName, cancellationToken)
                    .ConfigureAwait(false);
            var updates =
                DynamoTableDefinitionBuilder.BuildMissingGlobalSecondaryIndexUpdates(
                    table,
                    existing);
            for (var i = 0; i < updates.Count; i++)
            {
                await clientWrapper
                    .Client
                    .UpdateTableAsync(
                        new UpdateTableRequest
                        {
                            TableName = table.TableName,
                            AttributeDefinitions =
                                requestsByName[table.TableName].AttributeDefinitions,
                            GlobalSecondaryIndexUpdates = [updates[i]]
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
                changed = true;
                if (lifecycleOptions.WaitForCompletion || i < updates.Count - 1)
                    await WaitUntilTableActiveAsync(table.TableName, cancellationToken)
                        .ConfigureAwait(false);
            }
        }

        return (changed, createdTables);
    }

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

    private async Task<TableDescription> WaitUntilTableActiveAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        var options = GetTableLifecycleOptions();
        var started = TimeProvider.System.GetTimestamp();
        var delay = options.InitialPollingDelay;

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
                if (table.TableStatus == TableStatus.ACTIVE
                    && (table.GlobalSecondaryIndexes ?? []).All(static index
                        => index.IndexStatus == IndexStatus.ACTIVE))
                    return table;
            }
            catch (ResourceNotFoundException)
            {
                // Creation race: keep polling.
            }

            await DelayLifecyclePollAsync(options, started, delay, cancellationToken)
                .ConfigureAwait(false);
            delay = NextPollingDelay(delay, options.MaxPollingDelay, options.BackoffMultiplier);
        }
    }

    private async Task WaitUntilTableDeletedAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        var options = GetTableLifecycleOptions();
        var started = TimeProvider.System.GetTimestamp();
        var delay = options.InitialPollingDelay;

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

            await DelayLifecyclePollAsync(options, started, delay, cancellationToken)
                .ConfigureAwait(false);
            delay = NextPollingDelay(delay, options.MaxPollingDelay, options.BackoffMultiplier);
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
        var nextTicks = (long)Math.Ceiling(current.Ticks * multiplier);
        if (nextTicks <= current.Ticks)
            nextTicks = current.Ticks + 1;

        return TimeSpan.FromTicks(Math.Min(nextTicks, maximum.Ticks));
    }
}
