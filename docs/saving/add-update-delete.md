---
title: Add, Update, and Delete
description: How SaveChangesAsync translates EF Core entity states to PartiQL INSERT, UPDATE, and DELETE statements.
---

# Add, Update, and Delete

_`SaveChangesAsync` reads the EF Core change tracker, compiles each pending entity state change into a PartiQL statement, and sends the write to DynamoDB — only modified properties are included in UPDATE statements, and every write is a conditional expression against the item's primary key._

!!! warning "Async only"

    The DynamoDB SDK does not expose synchronous write APIs. `SaveChanges` (synchronous) always
    throws `NotSupportedException`. Always use `SaveChangesAsync`.

## How Writes Are Compiled

When you call `SaveChangesAsync`, the provider runs a two-stage pipeline before any network call
is made:

1. **`DynamoSaveChangesPlanner`** walks the change tracker and compiles each pending entity into
    a PartiQL statement and a parameter list. Statements are validated at this stage — if a
    statement exceeds the 8,192-byte limit, an exception is thrown before any write is attempted.
1. **`DynamoWriteExecutor`** sends the compiled statements to DynamoDB via the
    `ExecuteStatement`, `ExecuteTransaction`, or `BatchExecuteStatement` APIs depending on the
    number of operations and the configured transaction behavior (see [Transactions](transactions.md)).

## Adding Entities

Add an entity to a `DbSet` and call `SaveChangesAsync`. The entity must have all primary key
properties populated; if you are using `GeneratedKeyProperties`, the provider assigns values
before saving.

```csharp
var order = new Order
{
    Pk = "CUSTOMER#42",
    Sk = "ORDER#2026-001",
    Status = "pending",
    Total = 149.99m,
};

db.Orders.Add(order);
await db.SaveChangesAsync(cancellationToken);
```

The provider generates a PartiQL `INSERT INTO … VALUE {…}` statement. All mapped scalar
properties are included in the `VALUE` clause as positional parameters:

```sql
INSERT INTO "Orders"
VALUE {
    'pk': ?,
    'sk': ?,
    'status': ?,
    'total': ?
}
```

INSERT statements are **unconditional** — there is no existence check. If an item with the same
partition key and sort key already exists in the table, DynamoDB raises an error that the
provider maps to `DbUpdateException`:

```csharp
try
{
    db.Orders.Add(new Order { Pk = "CUSTOMER#42", Sk = "ORDER#2026-001", ... });
    await db.SaveChangesAsync(cancellationToken);
}
catch (DbUpdateException ex)
{
    // Item with this PK+SK already exists.
    // ex.InnerException is DuplicateItemException.
}
```

!!! note

    `DbUpdateException` (not `DbUpdateConcurrencyException`) is thrown for duplicate key
    violations because the item already existing is a uniqueness constraint failure, not a
    stale-read conflict. See [Optimistic Concurrency](concurrency.md) for the distinction.

If your application cannot guarantee key uniqueness at the application layer, perform a
read-before-write to check existence before adding, or use a conditional write pattern at the
DynamoDB level.

## Updating Entities

Load an entity, mutate its properties, and call `SaveChangesAsync`. EF Core's change tracker
records which properties changed from their snapshot values.

```csharp
var order = await db.Orders
    .SingleAsync(o => o.Pk == "CUSTOMER#42" && o.Sk == "ORDER#2026-001", cancellationToken);

order.Status = "shipped";
order.ShippedAt = DateTimeOffset.UtcNow;
order.LegacyField = null;   // setting to null removes the DynamoDB attribute

await db.SaveChangesAsync(cancellationToken);
```

The provider generates a `UPDATE … SET … REMOVE … WHERE pk = ? AND sk = ?` statement that
includes only the properties that actually changed:

```sql
UPDATE "Orders"
SET "status" = ?, "shippedAt" = ?
REMOVE "legacyField"
WHERE "pk" = ? AND "sk" = ?
```

Key behaviors:

- **Only modified properties appear.** Unchanged properties are omitted from the statement
    entirely — there is no full-document replace.
- **`null` writes `REMOVE`, not `NULL`.** Setting a scalar property to `null` removes the
    attribute from the DynamoDB item entirely (DynamoDB `MISSING`), rather than writing a
    `{ NULL: true }` attribute. If you need an explicit `NULL` attribute in DynamoDB, use an
    `EF.Functions.DynamoNull` wrapper (see [Limitations](../limitations.md)).
- **Primary keys cannot be modified.** Attempting to change a `[Key]`-annotated or key-mapped
    property on a tracked entity throws `NotSupportedException`. To change an item's key, delete
    the existing entity and add a new one.

### Owned Types in Updates

Owned navigations are stored as nested attributes (sub-documents) in the same DynamoDB item.
When an owned navigation changes, the provider emits a targeted SET or REMOVE clause rather
than replacing the entire item:

| Owned navigation change                   | Generated clause                         |
| ----------------------------------------- | ---------------------------------------- |
| Owned navigation added                    | `SET "address" = ?` (full sub-document)  |
| Owned navigation removed                  | `REMOVE "address"`                       |
| Property inside owned navigation modified | `SET "address"."city" = ?` (nested path) |

This is different from relational providers, where owned types are rows in related or
shadow tables. In the DynamoDB provider, every owned navigation mutation targets a path within
the same item.

## Deleting Entities

Call `db.Remove(entity)` (or set `db.Entry(entity).State = EntityState.Deleted`) and then call
`SaveChangesAsync`.

```csharp
var order = await db.Orders
    .SingleAsync(o => o.Pk == "CUSTOMER#42" && o.Sk == "ORDER#2026-001", cancellationToken);

db.Orders.Remove(order);
await db.SaveChangesAsync(cancellationToken);
```

The provider generates a `DELETE FROM … WHERE pk = ? AND sk = ?` statement:

```sql
DELETE FROM "Orders"
WHERE "pk" = ? AND "sk" = ?
```

**Deleting a non-existent item is a silent success.** DynamoDB returns success when the item
identified by the WHERE predicate is not found. This is by design: the goal of a DELETE is for
the item to not exist; if it is already gone, the outcome is the same. The provider does not
treat this as an error.

If you have configured concurrency tokens, the token value is appended to the WHERE predicate.
If the item exists but its token has changed since the entity was loaded, DynamoDB raises a
conflict and the provider throws `DbUpdateConcurrencyException`. See
[Optimistic Concurrency](concurrency.md).

## Statement Size Limit

Each PartiQL statement has an **8,192-byte** size limit enforced by DynamoDB. The provider
validates the compiled statement length before executing any writes and throws
`InvalidOperationException` at planning time if the limit is exceeded.

!!! warning "Statement size limit"

    The limit is most likely to be hit on INSERT statements with many mapped properties or large
    owned-type sub-documents. The error message reports the actual character or byte count:

    ```
    The generated PartiQL statement is 9,841 UTF-8 bytes, which exceeds DynamoDB's
    8,192-byte statement-size limit. Consider reducing the number of mapped scalar
    properties or splitting the write unit across multiple SaveChanges calls.
    ```

    To fix: reduce the number of mapped properties, split large owned types into separate items,
    or batch smaller sets of entities per `SaveChangesAsync` call.

## See also

- [Transactions](transactions.md)
- [Optimistic Concurrency](concurrency.md)
- [Limitations](../limitations.md)
