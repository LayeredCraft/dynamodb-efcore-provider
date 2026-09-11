---
title: Add, Update, and Delete
description: How SaveChangesAsync translates EF Core entity states to PartiQL INSERT, UPDATE, and DELETE statements.
---

# Add, Update, and Delete

_`SaveChangesAsync` reads the EF Core change tracker, compiles each pending entity state change into a PartiQL statement, and sends the write to DynamoDB — only modified properties are included in UPDATE statements, and UPDATE/DELETE statements target the item's primary key._

!!! warning "Async only"

    The DynamoDB SDK does not expose synchronous write APIs. `SaveChanges` (synchronous) always
    throws `NotSupportedException`. Always use `SaveChangesAsync`.

## How Writes Are Compiled

When you call `SaveChangesAsync`, the provider runs a two-stage pipeline before any network call
is made:

1. **`DynamoSaveChangesPlanner`** walks the change tracker and compiles each pending entity into
    a PartiQL statement and a parameter list. Statements are validated at this stage — if a
    statement exceeds the 8,192-byte limit, an exception is thrown before any write is attempted.
2. **`DynamoWriteExecutor`** sends the compiled statements to DynamoDB via the
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
    '$type': ?,
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
    .Where(o => o.Pk == "CUSTOMER#42" && o.Sk == "ORDER#2026-001")
    .AsAsyncEnumerable()
    .SingleAsync(cancellationToken);

order.Status = "shipped";
order.ShippedAt = DateTimeOffset.UtcNow;
order.LegacyField = null;   // scalar null writes a DynamoDB NULL attribute

await db.SaveChangesAsync(cancellationToken);
```

The provider generates an `UPDATE … SET … WHERE pk = ? AND sk = ?` statement that includes only
the properties that actually changed:

```sql
UPDATE "Orders"
SET "status" = ?, "shippedAt" = ?, "legacyField" = ?
WHERE "pk" = ? AND "sk" = ?
```

Key behaviors:

- **Only modified properties appear.** Unchanged properties are omitted from the statement
    entirely — there is no full-document replace.
- **Scalar `null` writes DynamoDB `NULL`.** Setting a scalar property to `null` writes an
    explicit `{ NULL: true }` attribute. Null complex properties and complex collections can be
    represented as removed nested attributes when the provider emits a `REMOVE` for that path.
- **Finalized table keys cannot be modified.** Attempting to change the EF primary key that maps to the DynamoDB partition key or sort key throws `NotSupportedException`. To change an item's key, delete the existing entity and add a new one.

### Complex Properties in Updates

Complex properties and complex collections are stored as nested attributes (sub-documents) in the
same DynamoDB item. When a complex-property path changes, the provider emits a targeted SET or
REMOVE clause rather
than replacing the entire item:

| Complex-property change                   | Generated clause                         |
| ----------------------------------------- | ---------------------------------------- |
| Complex property added                    | `SET "address" = ?` (full sub-document)  |
| Complex property removed                  | `REMOVE "address"`                       |
| Property inside complex property modified | `SET "address"."city" = ?` (nested path) |

This is different from relational providers, where related data may live in separate rows or
tables. In the DynamoDB provider, every complex-property mutation targets a path within the same
item.

Primitive collections inside complex properties are serialized using the same DynamoDB wire shapes
as root properties: lists become `L`, dictionaries become `M`, string/number/binary sets become
`SS`/`NS`/`BS`, and `byte[]` remains a binary scalar (`B`). Null list elements or dictionary
values serialize as DynamoDB `NULL`; null complex collections are removed when updated. DynamoDB
sets cannot be empty, contain null elements, or mix string, number, and binary member kinds.

## Deleting Entities

Call `db.Remove(entity)` (or set `db.Entry(entity).State = EntityState.Deleted`) and then call
`SaveChangesAsync`.

```csharp
var order = await db.Orders
    .Where(o => o.Pk == "CUSTOMER#42" && o.Sk == "ORDER#2026-001")
    .AsAsyncEnumerable()
    .SingleAsync(cancellationToken);

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

## ExecuteUpdateAsync

`ExecuteUpdateAsync` applies a single-item update directly against the table without loading the
entity into the change tracker. It executes immediately — there is no `SaveChangesAsync` step and
the change tracker is never consulted or updated.

```csharp
var affected = await db.Orders
    .Where(o => o.Pk == "CUSTOMER#42" && o.Sk == "ORDER#2026-001")
    .ExecuteUpdateAsync(setters
        => setters
            .SetProperty(o => o.Status, "shipped")
            .SetProperty(o => o.ShippedAt, DateTimeOffset.UtcNow),
        cancellationToken);
```

The provider generates one PartiQL statement:

```sql
UPDATE "Orders"
SET "status" = ?, "shippedAt" = ?
WHERE "pk" = 'CUSTOMER#42' AND "sk" = 'ORDER#2026-001'
```

Key behaviors:

- **The WHERE clause must equality-constrain the full primary key.** `ExecuteUpdateAsync` targets
    one item: the predicate must contain a partition-key equality, plus a sort-key equality when
    the entity has a sort key. Filters on other attributes are allowed as additional predicates.
    Queries without key equality (or with `IN`, range comparisons, or OR touching a key) throw at
    translation time.
- **The result is 0 or 1.** A successful update returns `1`. When a key-targeted `UPDATE`
    matches no item, DynamoDB Local raises a condition failure and the provider maps that to `0`
    instead of throwing. This behavior is verified against DynamoDB Local; the real service may
    differ — a non-matching key-targeted `UPDATE` can complete silently, in which case the
    affected count is `1`. There is no multi-row count.
- **Nothing touches the change tracker.** Tracked entities are not read or updated. If the
    tracked instance is still in scope, re-query it to observe the new values.
- **No implicit transaction.** The update is a single statement, so no transaction wrapper is
    involved and none can be combined with other writes.
- **Nested paths are supported.** `SetProperty(o => o.Address.City, "Seattle")` writes the nested
    attribute path (`SET "address"."city" = ?`).
- **Numeric self-reference supports `+` and `-` only.** `SetProperty(o => o.Count, o => o.Count + 1)`
    becomes `SET "count" = "count" + 1`, evaluated server-side against the stored value.
    Multiplication, division, and string concatenation are rejected: DynamoDB PartiQL SET
    expressions support numeric addition and subtraction only (see the
    [AWS PartiQL UPDATE reference](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.update.html)).
- **`null` becomes `SET attr = ?` with a DynamoDB `NULL` parameter.** Clearing an attribute with
    `SetProperty(o => o.Note, (string?)null)` writes an explicit NULL attribute, matching
    `SaveChangesAsync` scalar-null behavior.
- **Synchronous `ExecuteUpdate` is not supported.** DynamoDB execution is asynchronous only; use
    `ExecuteUpdateAsync`.
- **Setter targets must be mapped scalar properties.** Navigations and whole complex properties
    cannot be assigned; assign the leaf scalar of a complex-property path instead. Key properties
    cannot be mutated.

## ExecuteDeleteAsync

`ExecuteDeleteAsync` deletes one key-targeted item directly, without loading or synchronizing the
change tracker.

```csharp
var affected = await db.Orders
    .Where(o => o.Pk == "CUSTOMER#42" && o.Sk == "ORDER#2026-001")
    .ExecuteDeleteAsync(cancellationToken);
```

It generates one PartiQL statement:

```sql
DELETE FROM "Orders"
WHERE "pk" = 'CUSTOMER#42' AND "sk" = 'ORDER#2026-001'
```

The full-primary-key, base-table, async-only restrictions are the same as `ExecuteUpdateAsync`.
Extra non-key predicates are allowed. The result is `1` when DynamoDB Local deletes a matching
item and `0` for a missing item or a failed extra predicate; the real service may report `1` for a
non-matching key-targeted DELETE. It does not create an implicit transaction or update tracked
entities. See the [AWS PartiQL DELETE reference](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.delete.html).

## Statement Size Limit

Each PartiQL statement has an **8,192-byte** size limit enforced by DynamoDB. The provider
validates the compiled statement length before executing any writes and throws
`InvalidOperationException` at planning time if the limit is exceeded.

!!! warning "Statement size limit"

    The limit is most likely to be hit on INSERT statements with many mapped properties or large
    complex-property sub-documents. The error message reports the actual character or byte count:

    ```
    The generated PartiQL statement is 9,841 UTF-8 bytes, which exceeds DynamoDB's
    8,192-byte statement-size limit. Consider reducing the number of mapped scalar
    properties or splitting the write unit across multiple SaveChanges calls.
    ```

    To fix: reduce the number of mapped properties, split large nested documents into separate items,
    or batch smaller sets of entities per `SaveChangesAsync` call.

## See also

- [Transactions](transactions.md)
- [Optimistic Concurrency](concurrency.md)
- [Limitations](../limitations.md)
