using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Diagnostics.Internal;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Update;

#pragma warning disable EF1001 // Internal EF Core API: InternalEntityEntry.SetProperty

namespace EntityFrameworkCore.DynamoDb.Storage;

internal sealed class DynamoWriteExecutor(
    IDbContextOptions dbContextOptions,
    ICurrentDbContext currentDbContext,
    DynamoTransactionRuntimeOptions transactionRuntimeOptions,
    IDynamoClientWrapper clientWrapper,
    IDiagnosticsLogger<DbLoggerCategory.Database.Command> commandLogger,
    DynamoWriteExceptionMapper exceptionMapper)
{
    private readonly DynamoDbOptionsExtension _optionsExtension =
        dbContextOptions.FindExtension<DynamoDbOptionsExtension>()
        ?? new DynamoDbOptionsExtension();

    public async Task ExecuteAsync(DynamoWritePlan plan, CancellationToken cancellationToken)
    {
        var operations = plan.Operations;
        if (operations.Count == 0)
            return;

        var autoTransactionBehavior = currentDbContext.Context.Database.AutoTransactionBehavior;
        var effectiveTransactionOverflowBehavior =
            transactionRuntimeOptions.TransactionOverflowBehaviorOverride
            ?? _optionsExtension.TransactionOverflowBehavior;
        var effectiveMaxTransactionSize = transactionRuntimeOptions.MaxTransactionSizeOverride
            ?? _optionsExtension.MaxTransactionSize;
        var effectiveMaxBatchWriteSize = transactionRuntimeOptions.MaxBatchWriteSizeOverride
            ?? _optionsExtension.MaxBatchWriteSize;

        if (!ShouldUseTransaction(autoTransactionBehavior, operations.Count))
        {
            if (operations.Count == 1)
            {
                await ExecuteIndependentWritesAsync(operations, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            if (transactionRuntimeOptions.AcceptAllChangesOnSuccess == false)
                throw CreateNonAtomicBatchAcceptAllChangesRequiredException();

            var nonAtomicRootAggregateEntries =
                BuildRootAggregateEntries(plan.Entries, plan.RootEntries);
            await ExecuteChunkedBatchWritesAsync(
                    operations,
                    nonAtomicRootAggregateEntries,
                    effectiveMaxBatchWriteSize,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (operations.Count <= effectiveMaxTransactionSize)
        {
            ValidateTransactionalDuplicateTargets(operations);
            await ExecuteTransactionalWritesAsync(operations, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (autoTransactionBehavior == AutoTransactionBehavior.Always)
            throw CreateAlwaysOverflowException(operations.Count, effectiveMaxTransactionSize);

        if (effectiveTransactionOverflowBehavior == TransactionOverflowBehavior.Throw)
            throw CreateOverflowExecutionException(
                operations.Count,
                effectiveMaxTransactionSize,
                autoTransactionBehavior,
                effectiveTransactionOverflowBehavior);

        if (transactionRuntimeOptions.AcceptAllChangesOnSuccess == false)
            throw CreateChunkingAcceptAllChangesRequiredException();

        var rootAggregateEntries = BuildRootAggregateEntries(plan.Entries, plan.RootEntries);

        await ExecuteChunkedTransactionalWritesAsync(
                operations,
                rootAggregateEntries,
                effectiveMaxTransactionSize,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool ShouldUseTransaction(
        AutoTransactionBehavior autoTransactionBehavior,
        int operationCount)
        => autoTransactionBehavior switch
        {
            AutoTransactionBehavior.Never => false,
            AutoTransactionBehavior.WhenNeeded => operationCount > 1,
            AutoTransactionBehavior.Always => operationCount > 1,
            _ => throw new InvalidOperationException(
                $"Invalid AutoTransactionBehavior: {autoTransactionBehavior}"),
        };

    private static InvalidOperationException CreateAlwaysOverflowException(
        int operationCount,
        int effectiveMaxTransactionSize)
        => new(
            "SaveChanges cannot satisfy AutoTransactionBehavior.Always because the "
            + $"write unit contains {operationCount} root operations, exceeding the "
            + $"effective MaxTransactionSize of {effectiveMaxTransactionSize}."
            + " A single atomic transaction cannot represent this save operation.");

    private static InvalidOperationException CreateOverflowExecutionException(
        int operationCount,
        int effectiveMaxTransactionSize,
        AutoTransactionBehavior autoTransactionBehavior,
        TransactionOverflowBehavior effectiveTransactionOverflowBehavior)
        => new(
            "SaveChanges cannot satisfy transactional execution because the write unit "
            + $"contains {operationCount} root operations, exceeding the effective "
            + $"MaxTransactionSize of {effectiveMaxTransactionSize}. "
            + $"Current AutoTransactionBehavior is '{autoTransactionBehavior}' and "
            + $"TransactionOverflowBehavior is '{effectiveTransactionOverflowBehavior}'.");

    private static InvalidOperationException CreateChunkingAcceptAllChangesRequiredException()
        => new(
            "Chunked transactional SaveChanges is not supported when "
            + "acceptAllChangesOnSuccess is false. Partial chunk commits require "
            + "per-chunk tracker acceptance to avoid replaying already-persisted "
            + "writes on retry.");

    private static InvalidOperationException CreateNonAtomicBatchAcceptAllChangesRequiredException()
        => new(
            "Non-atomic batched SaveChanges is not supported when "
            + "acceptAllChangesOnSuccess is false. Partial batch commits require "
            + "per-batch tracker acceptance to avoid replaying already-persisted "
            + "writes on retry.");

    private async Task ExecuteIndependentWritesAsync(
        IReadOnlyList<CompiledWriteOperation> operations,
        CancellationToken cancellationToken)
    {
        foreach (var operation in operations)
        {
            commandLogger.ExecutingPartiQlWrite(operation.TableName, operation.Statement);

            try
            {
                await clientWrapper
                    .ExecuteWriteAsync(operation.Statement, operation.Parameters, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is DuplicateItemException
                    or ConditionalCheckFailedException
                    or TransactionCanceledException
                || exceptionMapper.IsDuplicateKeyException(ex))
            {
                throw exceptionMapper.WrapWriteException(
                    ex,
                    operation.EntityState,
                    operation.Entry);
            }
        }
    }

    private async Task ExecuteTransactionalWritesAsync(
        IReadOnlyList<CompiledWriteOperation> operations,
        CancellationToken cancellationToken)
    {
        foreach (var operation in operations)
            commandLogger.ExecutingPartiQlWrite(operation.TableName, operation.Statement);

        var statements = operations
            .Select(static operation => new ParameterizedStatement
            {
                Statement = operation.Statement, Parameters = operation.Parameters,
            })
            .ToList();

        try
        {
            await clientWrapper
                .ExecuteTransactionAsync(statements, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TransactionCanceledException tce)
        {
            throw exceptionMapper.WrapWriteException(
                tce,
                operations[0].EntityState,
                operations.Select(static x => x.Entry).ToList());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DbUpdateException(
                "Atomic SaveChanges transaction failed while executing DynamoDB ExecuteTransaction.",
                ex,
                operations.Select(static x => x.Entry).ToList());
        }
    }

    private async Task ExecuteChunkedTransactionalWritesAsync(
        IReadOnlyList<CompiledWriteOperation> operations,
        IReadOnlyDictionary<InternalEntityEntry, IReadOnlyList<IUpdateEntry>> rootAggregateEntries,
        int maxTransactionSize,
        CancellationToken cancellationToken)
    {
        foreach (var chunk in operations.Chunk(maxTransactionSize))
        {
            ValidateTransactionalDuplicateTargets(chunk);
            await ExecuteTransactionalWritesAsync(chunk, cancellationToken).ConfigureAwait(false);
            AcceptChunkEntries(chunk, rootAggregateEntries);
        }
    }

    private async Task ExecuteChunkedBatchWritesAsync(
        IReadOnlyList<CompiledWriteOperation> operations,
        IReadOnlyDictionary<InternalEntityEntry, IReadOnlyList<IUpdateEntry>> rootAggregateEntries,
        int maxBatchWriteSize,
        CancellationToken cancellationToken)
    {
        foreach (var chunk in operations.Chunk(maxBatchWriteSize))
        {
            foreach (var operation in chunk)
                commandLogger.ExecutingPartiQlWrite(operation.TableName, operation.Statement);

            var statements = chunk
                .Select(static operation => new BatchStatementRequest
                {
                    Statement = operation.Statement, Parameters = operation.Parameters,
                })
                .ToList();

            IReadOnlyList<BatchStatementResponse> responses;

            try
            {
                responses = await clientWrapper
                    .ExecuteBatchWriteAsync(statements, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new DbUpdateException(
                    "Non-atomic SaveChanges batch failed while executing DynamoDB "
                    + "BatchExecuteStatement.",
                    ex,
                    chunk.Select(static x => x.Entry).ToList());
            }

            if (responses.Count != chunk.Length)
                throw new DbUpdateException(
                    "DynamoDB BatchExecuteStatement returned an unexpected number of "
                    + "responses. SaveChanges cannot reconcile partial commit state safely.",
                    null,
                    chunk.Select(static x => x.Entry).ToList());

            var successfulOperations = new List<CompiledWriteOperation>(chunk.Length);
            List<IUpdateEntry>? failedEntries = null;
            EntityState firstFailedState = default;
            Exception? firstFailure = null;

            for (var i = 0; i < chunk.Length; i++)
            {
                var response = responses[i];
                if (response.Error is null)
                {
                    successfulOperations.Add(chunk[i]);
                    continue;
                }

                failedEntries ??= [];
                failedEntries.Add(chunk[i].Entry);

                if (firstFailure is null)
                {
                    firstFailure = CreateBatchStatementException(response.Error);
                    firstFailedState = chunk[i].EntityState;
                }
            }

            if (successfulOperations.Count > 0)
                AcceptChunkEntries(successfulOperations, rootAggregateEntries);

            if (failedEntries is not null)
                throw exceptionMapper.WrapWriteException(
                    firstFailure!,
                    firstFailedState,
                    failedEntries);
        }
    }

    private static AmazonDynamoDBException CreateBatchStatementException(BatchStatementError error)
        => new(error.Message ?? "DynamoDB BatchExecuteStatement reported a statement failure.")
        {
            ErrorCode = error.Code,
        };

    private static IReadOnlyDictionary<InternalEntityEntry, IReadOnlyList<IUpdateEntry>>
        BuildRootAggregateEntries(
            IList<IUpdateEntry> entries,
            IReadOnlyList<IUpdateEntry> rootEntries)
    {
        var rootToEntries =
            new Dictionary<InternalEntityEntry, HashSet<IUpdateEntry>>(
                ReferenceEqualityComparer.Instance);

        foreach (var rootEntry in rootEntries)
        {
            var internalRoot = (InternalEntityEntry)rootEntry;
            rootToEntries[internalRoot] =
                new HashSet<IUpdateEntry>(ReferenceEqualityComparer.Instance) { rootEntry };
        }

        foreach (var entry in entries)
        {
            var root = GetRootEntry((InternalEntityEntry)entry);
            if (!rootToEntries.TryGetValue(root, out var relatedEntries))
            {
                relatedEntries =
                    new HashSet<IUpdateEntry>(ReferenceEqualityComparer.Instance) { root };
                rootToEntries[root] = relatedEntries;
            }

            relatedEntries.Add(entry);
        }

        var result =
            new Dictionary<InternalEntityEntry, IReadOnlyList<IUpdateEntry>>(
                ReferenceEqualityComparer.Instance);

        foreach (var pair in rootToEntries)
            result[pair.Key] = pair.Value.ToList();

        return result;
    }

    private static void AcceptChunkEntries(
        IReadOnlyList<CompiledWriteOperation> chunk,
        IReadOnlyDictionary<InternalEntityEntry, IReadOnlyList<IUpdateEntry>> rootAggregateEntries)
    {
        var entriesToAccept = new HashSet<InternalEntityEntry>(ReferenceEqualityComparer.Instance);

        foreach (var operation in chunk)
        {
            var rootEntry = (InternalEntityEntry)operation.Entry;
            if (!rootAggregateEntries.TryGetValue(rootEntry, out var relatedEntries))
            {
                entriesToAccept.Add(rootEntry);
                continue;
            }

            foreach (var relatedEntry in relatedEntries)
                entriesToAccept.Add((InternalEntityEntry)relatedEntry);
        }

        foreach (var entry in entriesToAccept)
            entry.AcceptChanges();
    }

    private static void ValidateTransactionalDuplicateTargets(
        IReadOnlyList<CompiledWriteOperation> operations)
    {
        var duplicateTarget = operations
            .GroupBy(static x => x.TargetItem)
            .FirstOrDefault(static g => g.Count() > 1);

        if (duplicateTarget is null)
            return;

        throw new InvalidOperationException(
            "SaveChanges cannot satisfy transactional atomicity because the unit of work "
            + "contains multiple operations targeting the same DynamoDB item in a single "
            + "transaction, which is not allowed by ExecuteTransaction.");
    }

    private static InternalEntityEntry GetRootEntry(InternalEntityEntry entry)
    {
        while (true)
        {
            if (!entry.EntityType.IsOwned())
                return entry;

            var ownership = entry.EntityType.FindOwnership()
                ?? throw new InvalidOperationException(
                    $"Owned entity type '{entry.EntityType.DisplayName()}' has no ownership metadata.");

            var principal = entry.StateManager.FindPrincipal(entry, ownership)
                ?? throw new InvalidOperationException(
                    $"Owned entity '{entry.EntityType.DisplayName()}' is orphaned from its principal "
                    + $"'{ownership.PrincipalEntityType.DisplayName()}'.");

            if (principal.EntityType.IsOwned())
            {
                entry = principal;
                continue;
            }

            return principal;
        }
    }
}
