using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>Provides unsupported database lifecycle operations for the DynamoDB provider.</summary>
internal sealed class DynamoDatabaseCreator : IDatabaseCreator
{
    private const string DatabaseLifecycleNotSupported =
        "The DynamoDB database provider does not support database lifecycle operations.";

    /// <summary>Ensures the database is deleted.</summary>
    /// <returns>Never returns because database lifecycle operations are unsupported.</returns>
    /// <exception cref="NotSupportedException">
    ///     Always thrown because database lifecycle operations are
    ///     unsupported.
    /// </exception>
    public bool EnsureDeleted() => throw new NotSupportedException(DatabaseLifecycleNotSupported);

    /// <summary>Ensures the database is deleted asynchronously.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Never returns because database lifecycle operations are unsupported.</returns>
    /// <exception cref="NotSupportedException">
    ///     Always thrown because database lifecycle operations are
    ///     unsupported.
    /// </exception>
    public Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException(DatabaseLifecycleNotSupported);

    /// <summary>Ensures the database is created.</summary>
    /// <returns>Never returns because database lifecycle operations are unsupported.</returns>
    /// <exception cref="NotSupportedException">
    ///     Always thrown because database lifecycle operations are
    ///     unsupported.
    /// </exception>
    public bool EnsureCreated() => throw new NotSupportedException(DatabaseLifecycleNotSupported);

    /// <summary>Ensures the database is created asynchronously.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Never returns because database lifecycle operations are unsupported.</returns>
    /// <exception cref="NotSupportedException">
    ///     Always thrown because database lifecycle operations are
    ///     unsupported.
    /// </exception>
    public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException(DatabaseLifecycleNotSupported);

    /// <summary>Determines whether the database can be connected to.</summary>
    /// <returns>Never returns because database lifecycle operations are unsupported.</returns>
    /// <exception cref="NotSupportedException">
    ///     Always thrown because database lifecycle operations are
    ///     unsupported.
    /// </exception>
    public bool CanConnect() => throw new NotSupportedException(DatabaseLifecycleNotSupported);

    /// <summary>Determines whether the database can be connected to asynchronously.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Never returns because database lifecycle operations are unsupported.</returns>
    /// <exception cref="NotSupportedException">
    ///     Always thrown because database lifecycle operations are
    ///     unsupported.
    /// </exception>
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException(DatabaseLifecycleNotSupported);
}
