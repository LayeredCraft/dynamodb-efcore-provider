---
icon: lucide/shield-check
---

# Concurrency

## Explicit optimistic concurrency (manual tokens)

Optimistic concurrency is opt-in and follows EF Core's standard pattern.

Configure one or more properties as concurrency tokens:

```csharp
modelBuilder.Entity<CustomerItem>()
    .Property(x => x.Version)
    .IsConcurrencyToken();
```

For configured tokens, UPDATE and DELETE include original token values in the WHERE predicate:

```sql
WHERE "Pk" = ? AND "Version" = ?
```

If token values in DynamoDB have changed since the entity was read, DynamoDB raises
`ConditionalCheckFailedException`, which the provider maps to `DbUpdateConcurrencyException`.

## Manual token updates are required

This provider currently does not generate row-version values. Your application must assign the
new token value before calling `SaveChangesAsync`.

```csharp
var customer = await db.Customers.SingleAsync(x => x.Pk == pk && x.Sk == sk, ct);

customer.Email = "updated@example.com";
customer.Version += 1; // App-managed token mutation

await db.SaveChangesAsync(ct);
```

`IsRowVersion()` / `ValueGeneratedOnAddOrUpdate` is not supported yet and fails model validation.

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

`entry.ReloadAsync` re-reads the item from DynamoDB and resets tracked values (including
concurrency tokens) so the next `SaveChangesAsync` uses current store values.

## INSERT duplicate key → `DbUpdateException`

When an `Added` entity's primary key already exists in the table, DynamoDB raises
`DuplicateItemException`. The provider maps this to `DbUpdateException` (not
`DbUpdateConcurrencyException`, because it is a uniqueness constraint violation, not a concurrency
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

If the item still exists but its configured concurrency token values have changed,
`ConditionalCheckFailedException` is
raised and mapped to `DbUpdateConcurrencyException` as with UPDATE.
