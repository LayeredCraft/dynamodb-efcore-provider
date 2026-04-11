---
icon: lucide/shield-check
---

# Concurrency

## Provider-managed optimistic concurrency

Every root entity automatically carries a `$version` attribute managed by the provider. You do
not need to declare a version property or call `.IsConcurrencyToken()` — correct concurrency
behavior is the default.

| Operation | `$version` behavior                           |
| --------- | --------------------------------------------- |
| INSERT    | Written as `1`                                |
| UPDATE    | Incremented in SET; original checked in WHERE |
| DELETE    | Original checked in WHERE                     |

The WHERE predicate on UPDATE and DELETE looks like:

```sql
WHERE "Pk" = ? AND "$version" = ?
```

If the version in DynamoDB has advanced since the entity was read, DynamoDB raises
`ConditionalCheckFailedException`, which the provider maps to `DbUpdateConcurrencyException`.

## Handling `DbUpdateConcurrencyException`

When another writer modifies the same item between your read and your save, `SaveChangesAsync`
throws `DbUpdateConcurrencyException`. The standard retry pattern:

```csharp
async Task SaveWithRetry(MyDbContext db, CancellationToken ct)
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
            // Refresh stale entities from the store and retry.
            foreach (var entry in ex.Entries)
                await entry.ReloadAsync(ct);

            if (attempt == maxRetries - 1)
                throw;
        }
    }
}
```

`entry.ReloadAsync` re-reads the item from DynamoDB and resets the tracked values (including
`$version`) so the next `SaveChangesAsync` uses the current store version.

## INSERT duplicate key → `DbUpdateException`

When an `Added` entity's primary key already exists in the table, DynamoDB raises
`DuplicateItemException`. The provider maps this to `DbUpdateException` (not
`DbUpdateConcurrencyException`, because it is a uniqueness constraint violation, not a version
mismatch):

```csharp
try
{
    db.Items.Add(new Item { Pk = "ITEM#1", ... });
    await db.SaveChangesAsync(ct);
}
catch (DbUpdateException ex) when (ex.InnerException is DuplicateItemException)
{
    // Primary key already exists.
}
```

## DELETE missing-item behavior

Deleting an item that no longer exists in DynamoDB is a silent success — DynamoDB returns
success for a conditional DELETE when the condition evaluates on a missing item. This is by
design: the goal of a DELETE is for the item to not exist; if it is already gone, the outcome
is the same.

If the item still exists but its `$version` has changed, `ConditionalCheckFailedException` is
raised and mapped to `DbUpdateConcurrencyException` as with UPDATE.

## Items written before `$version` support

Items inserted into DynamoDB before `$version` support was added have no `$version` attribute.
On the first UPDATE via EF Core, the WHERE predicate uses `"$version" = 0`, which does not match
a missing attribute, and `DbUpdateConcurrencyException` is raised.

**Resolution:** Re-save those items once as Added entities so EF Core writes `$version = 1`.
After that first write, all subsequent updates and deletes are version-protected normally.

See also [Pre-`$version` items](limitations.md#pre-version-items) in Limitations.
