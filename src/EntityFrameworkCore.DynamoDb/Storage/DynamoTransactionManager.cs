using System.Transactions;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>Manages unsupported EF Core transactions for the DynamoDB provider.</summary>
internal sealed class DynamoTransactionManager
    : IDbContextTransactionManager, ITransactionEnlistmentManager
{
    private const string TransactionsNotSupported =
        "The DynamoDB database provider does not support explicit transactions.";

    /// <summary>Begins a transaction.</summary>
    /// <returns>Never returns because explicit transactions are unsupported.</returns>
    /// <exception cref="NotSupportedException">
    ///     Always thrown because explicit transactions are
    ///     unsupported.
    /// </exception>
    public IDbContextTransaction BeginTransaction()
        => throw new NotSupportedException(TransactionsNotSupported);

    /// <summary>Begins a transaction asynchronously.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Never returns because explicit transactions are unsupported.</returns>
    /// <exception cref="NotSupportedException">
    ///     Always thrown because explicit transactions are
    ///     unsupported.
    /// </exception>
    public Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException(TransactionsNotSupported);

    /// <summary>Commits the current transaction.</summary>
    /// <exception cref="NotSupportedException">
    ///     Always thrown because explicit transactions are
    ///     unsupported.
    /// </exception>
    public void CommitTransaction() => throw new NotSupportedException(TransactionsNotSupported);

    /// <summary>Commits the current transaction asynchronously.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Never returns because explicit transactions are unsupported.</returns>
    /// <exception cref="NotSupportedException">
    ///     Always thrown because explicit transactions are
    ///     unsupported.
    /// </exception>
    public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException(TransactionsNotSupported);

    /// <summary>Rolls back the current transaction.</summary>
    /// <exception cref="NotSupportedException">
    ///     Always thrown because explicit transactions are
    ///     unsupported.
    /// </exception>
    public void RollbackTransaction() => throw new NotSupportedException(TransactionsNotSupported);

    /// <summary>Rolls back the current transaction asynchronously.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Never returns because explicit transactions are unsupported.</returns>
    /// <exception cref="NotSupportedException">
    ///     Always thrown because explicit transactions are
    ///     unsupported.
    /// </exception>
    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException(TransactionsNotSupported);

    /// <summary>Gets the current ambient transaction.</summary>
    public Transaction? CurrentAmbientTransaction => null;

    /// <summary>Gets the current transaction.</summary>
    public IDbContextTransaction? CurrentTransaction => null;

    /// <summary>Gets the enlisted transaction.</summary>
    public Transaction? EnlistedTransaction => null;

    /// <summary>Enlists in the specified transaction.</summary>
    /// <param name="transaction">Transaction to enlist in.</param>
    /// <exception cref="NotSupportedException">
    ///     Always thrown because explicit transactions are
    ///     unsupported.
    /// </exception>
    public void EnlistTransaction(Transaction? transaction)
        => throw new NotSupportedException(TransactionsNotSupported);

    /// <summary>Resets manager state.</summary>
    public void ResetState() { }

    /// <summary>Resets manager state asynchronously.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Completed task.</returns>
    public Task ResetStateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
