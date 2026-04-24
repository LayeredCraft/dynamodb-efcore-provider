---
title: Saving Data
description: Overview of how SaveChangesAsync works and what write operations are supported.
icon: lucide/save
---

# Saving Data

_`SaveChangesAsync` reads the EF Core change tracker, compiles each pending entity state into a
PartiQL statement, and sends the writes to DynamoDB — with optional transaction wrapping,
configurable overflow behavior, and optimistic concurrency support._

!!! warning "Async only"

    The DynamoDB SDK does not expose synchronous write APIs. `SaveChanges` always throws
    `NotSupportedException`. Always use `SaveChangesAsync`.

## How It Works

When you call `SaveChangesAsync`, the provider runs a two-stage pipeline:

1. **Compile** — `DynamoSaveChangesPlanner` inspects every tracked entity and compiles its state
    into a PartiQL statement. Statements are validated before any network call is made.
1. **Execute** — `DynamoWriteExecutor` sends the compiled statements to DynamoDB using
    `ExecuteStatement` (single operation), `ExecuteTransaction` (multiple operations, atomic), or
    `BatchExecuteStatement` (multiple operations, non-atomic) depending on configuration.

The mapping from EF Core entity state to DynamoDB write operation:

| EF Entity State | PartiQL Statement                                             |
| --------------- | ------------------------------------------------------------- |
| `Added`         | `INSERT INTO "Table" VALUE {'pk': ?, ...}`                    |
| `Modified`      | `UPDATE "Table" SET "prop" = ? REMOVE "other" WHERE "pk" = ?` |
| `Deleted`       | `DELETE FROM "Table" WHERE "pk" = ?`                          |

Only modified properties appear in `UPDATE` statements — unchanged properties are omitted
entirely. Setting a property to `null` removes the attribute from DynamoDB (`REMOVE`), not
sets it to `{ NULL: true }`.

## In This Section

- **[Add, Update, and Delete](add-update-delete.md)** — How each entity state translates to a
    PartiQL statement, what the generated SQL looks like, how owned types are handled in updates,
    and the 8,192-byte statement size limit.
- **[Transactions](transactions.md)** — Auto-transaction wrapping via `ExecuteTransaction`,
    the 100-item limit, overflow behavior (throw vs. chunking), and non-transactional batch writes.
- **[Optimistic Concurrency](concurrency.md)** — Configuring concurrency tokens, manual token
    updates, conflict detection, and handling `DbUpdateConcurrencyException`.

## See also

- [Querying](../querying/index.md)
- [Limitations](../limitations.md)
