---
title: Transactions
description: How the provider wraps SaveChangesAsync in DynamoDB ExecuteTransaction calls, and how to configure overflow behavior for large write units.
---

# Transactions

_By default, the provider wraps every multi-entity `SaveChangesAsync` in a single DynamoDB
`ExecuteTransaction` call — all operations succeed or all are rolled back. When the number of
operations exceeds DynamoDB's 100-item limit, the overflow behavior is configurable._

## How DynamoDB Transactions Work

DynamoDB's `ExecuteTransaction` API accepts a list of PartiQL statements and executes them as a
single all-or-nothing unit. If any statement fails — due to a condition check, a throttle, or
any other error — the entire transaction is rolled back.

The provider maps a `SaveChangesAsync` call with multiple entities directly to a single
`ExecuteTransaction` call. Single-entity saves use `ExecuteStatement` directly (no transaction
overhead). There are no user-initiated transactions — there is no `BeginTransaction` API.

## Auto-Transaction Behavior

The provider respects EF Core's standard `AutoTransactionBehavior` property, which controls when
automatic transactions are applied:

| Value                  | Behavior                                                                                                                                                               |
| ---------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `WhenNeeded` (default) | Uses `ExecuteTransaction` when there is more than one operation; single operations use `ExecuteStatement` directly.                                                    |
| `Always`               | Enforces transactional execution for multi-operation saves; single operations still use `ExecuteStatement` directly. Throws if the count exceeds `MaxTransactionSize`. |
| `Never`                | Skips transactions entirely; uses `BatchExecuteStatement` instead. Operations execute independently — partial success is possible.                                     |

Set it on the `DatabaseFacade` instance, typically in the DbContext constructor or when
configuring the context:

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // For all instances of this context:
        Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
    }
}
```

## The 100-Item Limit

DynamoDB's `ExecuteTransaction` API allows at most **100 operations** per call. The provider
exposes this ceiling as `MaxTransactionSize` (default: 100, valid range: 1–100). You can lower
it — for example, to stay within a smaller capacity budget — but you cannot raise it above 100.

When a `SaveChangesAsync` call produces more operations than `MaxTransactionSize`, the behavior
depends on `TransactionOverflowBehavior`.

## Transaction Overflow Behavior

### Throw (default)

`SaveChangesAsync` throws `InvalidOperationException` when the operation count exceeds
`MaxTransactionSize`. This is the safe default: it prevents the partial-commit risk that
chunking introduces, and forces you to design write units that fit within a single transaction.

```
SaveChanges cannot satisfy transactional execution because the write unit contains
107 root operations, exceeding the effective MaxTransactionSize of 100.
Current AutoTransactionBehavior is 'WhenNeeded' and TransactionOverflowBehavior is 'Throw'.
```

### UseChunking

The write unit is split into sequential chunks, each up to `MaxTransactionSize` operations.
Each chunk is committed as a separate `ExecuteTransaction` call.

!!! warning "Partial-commit risk with chunking"

    Each chunk is individually atomic, but there is **no atomicity guarantee across chunks**.
    If chunk 2 fails after chunk 1 has already been committed, your data is left in a
    partially-saved state with no automatic rollback.

    Use `UseChunking` only when:

    - The entities in the write unit are logically independent (partial success is acceptable).
    - The operations are idempotent (a retry can safely re-apply committed chunks).

    Do not use `UseChunking` for write units where coupled invariants must all commit together
    (e.g., inventory reservation + order creation).

Chunking also requires that `SaveChangesAsync` is called with `acceptAllChangesOnSuccess: true`
(the default). Calling `SaveChangesAsync(acceptAllChangesOnSuccess: false)` with chunking
active throws, because the change tracker cannot safely reflect which chunks committed when
partial failure occurs.

## Configuration

### Startup configuration

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseDynamo(o => o
        .TransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking)
        .MaxTransactionSize(50)));
```

### Runtime override (per-context instance)

Use the extension methods on `DatabaseFacade` to override settings for a specific context
instance. These overrides take precedence over startup configuration:

```csharp
// Override for this context instance only
context.Database.SetTransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking);
context.Database.SetMaxTransactionSize(50);

await context.SaveChangesAsync(cancellationToken);
```

Runtime overrides are useful when a specific operation has different requirements than the
application default — for example, a bulk import that can tolerate partial success.

## Non-Transactional Batch Writes

When `AutoTransactionBehavior` is `Never`, the provider uses `BatchExecuteStatement` instead of
`ExecuteTransaction`. Each statement in the batch executes independently:

- A failure in one operation does not affect other operations.
- Operations can succeed or fail individually (partial success is expected behavior).
- The batch size limit is **25 operations** (configurable via `MaxBatchWriteSize`, range: 1–25).

```csharp
context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;

// Optionally tune the batch size:
context.Database.SetMaxBatchWriteSize(10);
```

Like chunking, non-transactional batching requires `acceptAllChangesOnSuccess: true` (the
default). The change tracker accepts per-batch results, so each successfully persisted batch
is reflected in the tracker even if a later batch fails.

Use `Never` only when the entities being saved are truly independent and your application can
handle partial write success — for example, bulk-importing unrelated records where each item
can be retried independently.

## Duplicate Operations on the Same Item

DynamoDB prohibits multiple operations targeting the **same partition key + sort key combination**
within a single `ExecuteTransaction` call. The provider detects this conflict before making the
network call and throws `InvalidOperationException`:

```
SaveChanges cannot satisfy transactional atomicity because the unit of work contains
multiple operations targeting the same DynamoDB item in a single transaction, which
is not allowed by ExecuteTransaction.
```

This situation arises from EF Core anti-patterns — for example, if the same entity is modified
and then deleted in the same unit of work. Each `SaveChangesAsync` call should represent a
coherent unit with at most one operation per item.

## Configuration Reference

| Setting                       | Default      | Range | API                                                                                                            |
| ----------------------------- | ------------ | ----- | -------------------------------------------------------------------------------------------------------------- |
| `AutoTransactionBehavior`     | `WhenNeeded` | —     | `context.Database.AutoTransactionBehavior` (EF Core standard)                                                  |
| `TransactionOverflowBehavior` | `Throw`      | —     | Builder: `.TransactionOverflowBehavior(...)` / Runtime: `context.Database.SetTransactionOverflowBehavior(...)` |
| `MaxTransactionSize`          | 100          | 1–100 | Builder: `.MaxTransactionSize(...)` / Runtime: `context.Database.SetMaxTransactionSize(...)`                   |
| `MaxBatchWriteSize`           | 25           | 1–25  | Builder: `.MaxBatchWriteSize(...)` / Runtime: `context.Database.SetMaxBatchWriteSize(...)`                     |

## See also

- [Add, Update, and Delete](add-update-delete.md)
- [Optimistic Concurrency](concurrency.md)
- [Limitations](../limitations.md)
