using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using EntityFrameworkCore.DynamoDb.Storage;
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
    /// <param name="databaseFacade">The database facade for the current DbContext.</param>
    /// <param name="behavior">The overflow behavior to apply for this context instance.</param>
    public static void SetTransactionOverflowBehavior(
        this DatabaseFacade databaseFacade,
        TransactionOverflowBehavior behavior)
        => GetRuntimeOptions(databaseFacade).TransactionOverflowBehaviorOverride = behavior;

    /// <summary>
    /// Gets the effective transaction overflow behavior for this context.
    /// </summary>
    /// <param name="databaseFacade">The database facade for the current DbContext.</param>
    /// <returns>The runtime override when present; otherwise the configured provider default.</returns>
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
    /// <param name="databaseFacade">The database facade for the current DbContext.</param>
    /// <param name="maxTransactionSize">Maximum operations per ExecuteTransaction call (1-100).</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="maxTransactionSize"/> is outside the supported range.
    /// </exception>
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
    /// <param name="databaseFacade">The database facade for the current DbContext.</param>
    /// <returns>The runtime override when present; otherwise the configured provider default.</returns>
    public static int GetMaxTransactionSize(this DatabaseFacade databaseFacade)
    {
        var runtimeOptions = GetRuntimeOptions(databaseFacade);

        return runtimeOptions.MaxTransactionSizeOverride
            ?? GetDynamoOptionsExtension(databaseFacade).MaxTransactionSize;
    }

    /// <summary>Sets a per-context override for max non-atomic batch write size.</summary>
    /// <param name="databaseFacade">The database facade for the current DbContext.</param>
    /// <param name="maxBatchWriteSize">Maximum operations per BatchExecuteStatement call (1-25).</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="maxBatchWriteSize"/> is outside the supported range.
    /// </exception>
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
    /// <param name="databaseFacade">The database facade for the current DbContext.</param>
    /// <returns>The runtime override when present; otherwise the configured provider default.</returns>
    public static int GetMaxBatchWriteSize(this DatabaseFacade databaseFacade)
    {
        var runtimeOptions = GetRuntimeOptions(databaseFacade);

        return runtimeOptions.MaxBatchWriteSizeOverride
            ?? GetDynamoOptionsExtension(databaseFacade).MaxBatchWriteSize;
    }

    /// <summary>Returns the underlying <see cref="IAmazonDynamoDB" /> client used by this context.</summary>
    /// <param name="databaseFacade">The <see cref="DatabaseFacade" /> for the current context.</param>
    /// <returns>The <see cref="IAmazonDynamoDB" /> client instance.</returns>
    public static IAmazonDynamoDB GetDynamoClient(this DatabaseFacade databaseFacade)
        => GetService<IDynamoClientWrapper>(databaseFacade).Client;

    private static TService GetService<TService>(IInfrastructure<IServiceProvider> databaseFacade)
        where TService : class
    {
        var service = databaseFacade.GetService<TService>();
        if (service == null)
            throw new InvalidOperationException(
                $"Service of type '{typeof(TService).FullName}' is not available.");

        return service;
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
