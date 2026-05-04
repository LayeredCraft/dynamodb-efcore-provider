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

        if (ex is ConditionalCheckFailedException ccf)
            return HasReturnedItem(ccf.Item)
                ? CreateConcurrencyException(ex, entityState, entries)
                : CreateMissingItemException(ex, entityState, entries);

        if (ex is TransactionCanceledException tce)
        {
            var conditionFailureReasons =
                tce
                    .CancellationReasons
                    ?.Where(static r
                        => string.Equals(
                            r.Code,
                            "ConditionalCheckFailed",
                            StringComparison.Ordinal))
                    .ToList();

            var hasConditionFailure = conditionFailureReasons?.Count > 0;
            if (hasConditionFailure)
                return conditionFailureReasons!.Any(static r => HasReturnedItem(r.Item))
                    ? CreateConcurrencyException(tce, entityState, entries)
                    : CreateMissingItemException(tce, entityState, entries);

            return new DbUpdateException(
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

    private static DbUpdateConcurrencyException CreateConcurrencyException(
        Exception ex,
        EntityState entityState,
        IReadOnlyList<IUpdateEntry> entries)
    {
        var firstEntry = entries[0];

        return new DbUpdateConcurrencyException(
            $"The '{firstEntry.EntityType.DisplayName()}' entity could not be "
            + (entityState == EntityState.Modified ? "updated" : "deleted")
            + " because one or more concurrency token values have changed since it was last read. "
            + "Another writer may have modified this item.",
            ex,
            entries);
    }

    private static DbUpdateException CreateMissingItemException(
        Exception ex,
        EntityState entityState,
        IReadOnlyList<IUpdateEntry> entries)
    {
        var firstEntry = entries[0];

        return new DbUpdateException(
            $"The '{firstEntry.EntityType.DisplayName()}' entity could not be "
            + (entityState == EntityState.Modified ? "updated" : "deleted")
            + " because the DynamoDB item was not found for its key. It may have been "
            + "deleted, or its key values may not match the stored item.",
            ex,
            entries);
    }

    private static bool HasReturnedItem(IReadOnlyDictionary<string, AttributeValue>? item)
        => item is { Count: > 0 };
}
