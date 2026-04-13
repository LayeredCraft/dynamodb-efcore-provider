namespace EntityFrameworkCore.DynamoDb.Infrastructure;

/// <summary>
/// Controls how SaveChanges handles transactional overflow when one DynamoDB transaction
/// cannot represent the full write unit.
/// </summary>
public enum TransactionOverflowBehavior
{
    /// <summary>
    /// Throws when a transactional SaveChanges unit exceeds the configured max transaction size.
    /// </summary>
    Throw,

    /// <summary>
    /// Splits overflowing transactional SaveChanges units into multiple ExecuteTransaction calls.
    /// </summary>
    UseChunking,
}
