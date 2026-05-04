---
title: Optimistic Concurrency
description: How to configure concurrency tokens and handle conflicts when multiple writers modify the same DynamoDB item.
---

# Optimistic Concurrency

_The provider implements optimistic concurrency by appending the original token value to the
WHERE predicate of UPDATE and DELETE statements — if another writer has changed the item since
it was loaded, DynamoDB rejects the write and the provider throws `DbUpdateConcurrencyException`._

## Configuring a Concurrency Token

Mark one or more properties as concurrency tokens using either the `[ConcurrencyToken]`
attribute or the Fluent API:

```csharp
// Attribute
public class Order
{
    public string Pk { get; set; }
    public string Sk { get; set; }
    public string Status { get; set; }

    [ConcurrencyToken]
    public int Version { get; set; }
}

// Fluent API (equivalent)
modelBuilder.Entity<Order>()
    .Property(x => x.Version)
    .IsConcurrencyToken();
```

For every property marked as a concurrency token, the provider appends an equality condition
to the WHERE clause of UPDATE and DELETE statements using the **original value** — the value the
property held when the entity was loaded from DynamoDB:

```sql
UPDATE "Orders"
SET "status" = ?
WHERE "pk" = ? AND "sk" = ? AND "version" = ?
--                              ^^^^^^^^^^^^^^ original version, not the new value
```

If the item in DynamoDB has a different `version` value than what was read — because another
writer updated it in the meantime — DynamoDB raises `ConditionalCheckFailedException`, which the
provider maps to `DbUpdateConcurrencyException`.

## Manual Token Updates Are Required

!!! note

    The provider does not auto-generate or increment concurrency token values.
    `IsRowVersion()` and `ValueGeneratedOnAddOrUpdate` are not yet supported.

Your application must assign the new token value before calling `SaveChangesAsync`. A common
pattern is an integer version that the writer increments on each save:

```csharp
var order = await db.Orders
    .AsAsyncEnumerable()
    .SingleAsync(o => o.Pk == pk && o.Sk == sk, cancellationToken);

order.Status = "shipped";
order.Version += 1; // must be updated before saving — the provider uses the original value in WHERE

await db.SaveChangesAsync(cancellationToken);
```

The change tracker snapshots the version at load time (e.g., `3`) and places that snapshot in
the WHERE clause. After the update succeeds, the new value (`4`) becomes the current snapshot
for subsequent saves.

## How Conflicts Are Detected

Conflict detection depends on the execution path:

- **Single-entity saves** (`ExecuteStatement`): DynamoDB returns `ConditionalCheckFailedException`
    when the WHERE predicate does not match.
- **Transactional saves** (`ExecuteTransaction`): DynamoDB returns `TransactionCanceledException`
    with a `ConditionalCheckFailed` cancellation reason for the conflicting item.

The provider requests DynamoDB's `ALL_OLD` value on condition-check failures and uses the
returned item to distinguish key misses from token conflicts:

- If DynamoDB returns the existing item, the key matched and the concurrency token predicate
    failed; the provider throws `DbUpdateConcurrencyException`.
- If DynamoDB does not return an item, the target key did not match any item. The item may have
    been deleted, or the tracked key values may not match the stored item; the provider throws
    `DbUpdateException`.

The `Entries` property on the exception identifies which entities were involved.

## Handling DbUpdateConcurrencyException

When a conflict is detected, the standard recovery pattern is to reload the entity from
DynamoDB and retry. `ReloadAsync` re-reads the item and resets all tracked values —
including the concurrency token — so the next save uses the current store state:

```csharp
async Task SaveWithRetry(AppDbContext db, CancellationToken ct)
{
    const int maxRetries = 3;

    for (var attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Reload all conflicting entries from DynamoDB.
            foreach (var entry in ex.Entries)
                await entry.ReloadAsync(ct);

            // Re-apply business logic if needed, then retry.
            if (attempt == maxRetries - 1)
                throw;
        }
    }
}
```

Note that `ReloadAsync` resets the entity to the current store values. If your business logic
depends on the values that were being written (e.g., incrementing a counter), you must
re-apply those mutations after reloading.

## INSERT Duplicate Key

When an `Added` entity's primary key already exists in DynamoDB, the insert fails with
`DuplicateItemException`. The provider maps this to `DbUpdateException`, not
`DbUpdateConcurrencyException`:

```csharp
try
{
    db.Orders.Add(new Order { Pk = "CUSTOMER#42", Sk = "ORDER#001", ... });
    await db.SaveChangesAsync(cancellationToken);
}
catch (DbUpdateException ex)
{
    // Item with this PK+SK already exists.
}
```

The distinction matters: `DbUpdateConcurrencyException` means a stale-read conflict (another
writer changed an item you already read). `DbUpdateException` on a duplicate key means the item
never existed in your read — it is a uniqueness violation, not a concurrency conflict. The
retry-with-reload pattern does not apply here.

## DELETE and Missing Items

Deleting an entity that no longer exists in DynamoDB is a **silent success** — DynamoDB returns
success when the WHERE predicate matches no item. The provider does not treat this as an error
because the goal of DELETE is for the item not to exist; if it is already gone, the outcome is
the same.

If concurrency tokens are configured, the token value is included in the WHERE predicate. This
creates an important asymmetry:

- **Item missing entirely** → silent success for singleton deletes (provider accepts the delete
    as done) or `DbUpdateException` if DynamoDB reports a failed condition on that write path.
- **Item present with a different token value** → `ConditionalCheckFailedException` with old item
    attributes → `DbUpdateConcurrencyException`.

The second case matters when two writers race to delete the same item: the first delete
succeeds and the item is gone; the second delete finds the item missing and also succeeds. But
if another writer *updates* the item (changing its token) before your delete runs, you get a
concurrency conflict — DynamoDB found the item but the token predicate didn't match.

## See also

- [Add, Update, and Delete](add-update-delete.md)
- [Transactions](transactions.md)
- [Limitations](../limitations.md)
