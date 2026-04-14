using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.DynamoDb.Storage;

internal sealed class DynamoWriteExceptionMapper
{
    public DbUpdateException WrapWriteException(
        Exception ex,
        EntityState entityState,
        IUpdateEntry entry)
        => WrapWriteException(ex, entityState, [entry]);

    public DbUpdateException WrapWriteException(
        Exception ex,
        EntityState entityState,
        IReadOnlyList<IUpdateEntry> entries)
    {
        var firstEntry = entries[0];

        if (ex is DuplicateItemException || IsDuplicateKeyException(ex))
            return new DbUpdateException(
                $"Cannot insert '{firstEntry.EntityType.DisplayName()}': an item with the same primary "
                + "key already exists.",
                ex,
                entries);

        if (ex is ConditionalCheckFailedException || IsConditionalCheckFailedException(ex))
            return new DbUpdateConcurrencyException(
                $"The '{firstEntry.EntityType.DisplayName()}' entity could not be "
                + (entityState == EntityState.Modified ? "updated" : "deleted")
                + " because one or more concurrency token values have changed since it was last read. "
                + "Another writer may have modified this item.",
                ex,
                entries);

        if (ex is TransactionCanceledException tce)
        {
            var hasConcurrency = tce.CancellationReasons?.Any(static r
                    => string.Equals(r.Code, "ConditionalCheckFailed", StringComparison.Ordinal))
                ?? false;

            return hasConcurrency
                ? new DbUpdateConcurrencyException(
                    $"Transaction cancelled due to a concurrency token conflict on "
                    + $"'{firstEntry.EntityType.DisplayName()}'.",
                    tce,
                    entries)
                : new DbUpdateException(
                    $"Transaction cancelled while saving '{firstEntry.EntityType.DisplayName()}'.",
                    tce,
                    entries);
        }

        return new DbUpdateException(
            $"An error occurred saving '{firstEntry.EntityType.DisplayName()}' to DynamoDB.",
            ex,
            entries);
    }

    public bool IsDuplicateKeyException(Exception ex)
        => ex is AmazonDynamoDBException ade
            && string.Equals(ade.ErrorCode, "DuplicateItem", StringComparison.Ordinal);

    private static bool IsConditionalCheckFailedException(Exception ex)
        => ex is AmazonDynamoDBException ade
            && (string.Equals(ade.ErrorCode, "ConditionalCheckFailed", StringComparison.Ordinal)
                || string.Equals(
                    ade.ErrorCode,
                    "ConditionalCheckFailedException",
                    StringComparison.Ordinal));
}
