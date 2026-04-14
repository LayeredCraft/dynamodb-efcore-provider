namespace EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

/// <summary>Scoped runtime overrides for transaction overflow execution behavior.</summary>
public sealed class DynamoTransactionRuntimeOptions
{
    /// <summary>
    /// Optional per-context override for transaction overflow behavior.
    /// </summary>
    public TransactionOverflowBehavior? TransactionOverflowBehaviorOverride { get; set; }

    /// <summary>
    /// Optional per-context override for max transaction size.
    /// </summary>
    public int? MaxTransactionSizeOverride { get; set; }

    /// <summary>Optional per-context override for max non-atomic batch write size.</summary>
    public int? MaxBatchWriteSizeOverride { get; set; }

    /// <summary>Captures <c>acceptAllChangesOnSuccess</c> for current SaveChanges call.</summary>
    public bool? AcceptAllChangesOnSuccess { get; set; }
}
