using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for provider-specific transaction overflow behavior on
/// <see cref="DatabaseFacade" />.
/// </summary>
public static class DynamoDatabaseFacadeExtensions
{
    /// <summary>
    /// Sets a per-context override for transaction overflow behavior.
    /// </summary>
    public static void SetTransactionOverflowBehavior(
        this DatabaseFacade databaseFacade,
        TransactionOverflowBehavior behavior)
        => GetRuntimeOptions(databaseFacade).TransactionOverflowBehaviorOverride = behavior;

    /// <summary>
    /// Gets the effective transaction overflow behavior for this context.
    /// </summary>
    public static TransactionOverflowBehavior GetTransactionOverflowBehavior(
        this DatabaseFacade databaseFacade)
    {
        var runtimeOptions = GetRuntimeOptions(databaseFacade);

        return runtimeOptions.TransactionOverflowBehaviorOverride
            ?? GetDynamoOptionsExtension(databaseFacade).TransactionOverflowBehavior;
    }

    /// <summary>
    /// Sets a per-context override for max transaction size.
    /// </summary>
    public static void SetMaxTransactionSize(
        this DatabaseFacade databaseFacade,
        int maxTransactionSize)
    {
        if (maxTransactionSize is <= 0 or > 100)
            throw new InvalidOperationException(
                $"The specified 'MaxTransactionSize' value '{maxTransactionSize}' is not valid. "
                + "It must be between 1 and 100.");

        GetRuntimeOptions(databaseFacade).MaxTransactionSizeOverride = maxTransactionSize;
    }

    /// <summary>
    /// Gets the effective max transaction size for this context.
    /// </summary>
    public static int GetMaxTransactionSize(this DatabaseFacade databaseFacade)
    {
        var runtimeOptions = GetRuntimeOptions(databaseFacade);

        return runtimeOptions.MaxTransactionSizeOverride
            ?? GetDynamoOptionsExtension(databaseFacade).MaxTransactionSize;
    }

    /// <summary>Sets a per-context override for max non-atomic batch write size.</summary>
    public static void SetMaxBatchWriteSize(
        this DatabaseFacade databaseFacade,
        int maxBatchWriteSize)
    {
        if (maxBatchWriteSize is <= 0 or > 25)
            throw new InvalidOperationException(
                $"The specified 'MaxBatchWriteSize' value '{maxBatchWriteSize}' is not valid. "
                + "It must be between 1 and 25.");

        GetRuntimeOptions(databaseFacade).MaxBatchWriteSizeOverride = maxBatchWriteSize;
    }

    /// <summary>Gets the effective max non-atomic batch write size for this context.</summary>
    public static int GetMaxBatchWriteSize(this DatabaseFacade databaseFacade)
    {
        var runtimeOptions = GetRuntimeOptions(databaseFacade);

        return runtimeOptions.MaxBatchWriteSizeOverride
            ?? GetDynamoOptionsExtension(databaseFacade).MaxBatchWriteSize;
    }

    private static DynamoTransactionRuntimeOptions GetRuntimeOptions(DatabaseFacade databaseFacade)
        => ((IDatabaseFacadeDependenciesAccessor)databaseFacade).Context
            .GetService<DynamoTransactionRuntimeOptions>();

    private static DynamoDbOptionsExtension GetDynamoOptionsExtension(DatabaseFacade databaseFacade)
        => ((IDatabaseFacadeDependenciesAccessor)databaseFacade)
            .Context
            .GetService<IDbContextOptions>()
            .FindExtension<DynamoDbOptionsExtension>()
            ?? throw new InvalidOperationException(
                "DynamoDB provider services are not available for this DbContext.");
}
