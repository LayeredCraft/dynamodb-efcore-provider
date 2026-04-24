---
title: DbContext Options
description: How to register and configure the provider in DbContext.
---

# DbContext Options

_Register the DynamoDB EF Core provider by calling `UseDynamo` on `DbContextOptionsBuilder`, then
tune transaction limits, batch sizes, and index selection behavior through the options builder or
per-context runtime overrides._

## Registering the Provider

### In OnConfiguring

Override `OnConfiguring` in your `DbContext` subclass. This is the quickest path and works well
for console apps or tests:

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.UseDynamo();
```

Pass a configuration callback to set provider options at the same time:

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.UseDynamo(options =>
    {
        options.ConfigureDynamoDbClientConfig(config =>
        {
            config.ServiceURL = "http://localhost:8000";
            config.AuthenticationRegion = "us-east-1";
            config.UseHttp = true;
        });
    });
```

### Through Dependency Injection

Pass `UseDynamo` inside `AddDbContext` when you want to supply a pre-configured
`IAmazonDynamoDB` instance or read options from `IConfiguration`:

```csharp
services.AddDbContext<ShopContext>(options =>
{
    options.UseDynamo(o =>
    {
        o.DynamoDbClient(dynamoClient);
    });
});
```

A context that configures itself via `OnConfiguring` can still be registered in the container —
the two approaches are not mutually exclusive:

```csharp
services.AddDbContext<ShopContext>(); // OnConfiguring handles provider setup
```

`AddEntityFrameworkDynamo()` is called automatically by `UseDynamo` and registers all provider
services. You do not need to call it directly.

## Available Options

All options are set on the `DynamoDbContextOptionsBuilder` passed to the `UseDynamo` callback.

### `TransactionOverflowBehavior`

When a transactional `SaveChangesAsync` write unit exceeds the effective `MaxTransactionSize`, the
provider applies the configured `TransactionOverflowBehavior`.

This option exists because DynamoDB caps a single `ExecuteTransaction` request at 100 write
operations. If your unit of work can cross that boundary (for example, aggregate updates that touch
many root entities at once), you need to decide whether the provider should fail before sending
anything or continue in smaller transactional chunks.

| Value             | Behavior                                                       |
| ----------------- | -------------------------------------------------------------- |
| `Throw` (default) | Throws `InvalidOperationException` before any writes are sent  |
| `UseChunking`     | Splits the write unit into multiple `ExecuteTransaction` calls |

Choose based on your consistency requirements:

- Use `Throw` when the whole save must behave as one all-or-nothing unit.
- Use `UseChunking` when large saves are expected and your application can recover from
    partially completed saves (idempotent commands, compensating actions, or explicit re-query +
    retry flows).

```csharp
optionsBuilder.UseDynamo(options =>
{
    options.TransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking);
});
```

!!! warning "Chunking is not globally atomic"

    Each chunk is individually atomic, but the overall `SaveChangesAsync` call is not. If chunk _N_
    fails, chunks _0..N-1_ are permanently committed and cannot be rolled back. Always re-query the
    affected entities before retrying a chunked save.

!!! warning "Chunking requires `acceptAllChangesOnSuccess=true`"

    Chunked transactional saves are not supported for `SaveChangesAsync(false, ...)`. The provider
    throws `InvalidOperationException` before any chunk is sent.

### `MaxTransactionSize`

`MaxTransactionSize` caps how many write operations the provider sends in a single
`ExecuteTransaction` call. DynamoDB's hard maximum is 100 and the provider default is 100:

This limit applies only to transactional multi-root save paths that use `ExecuteTransaction`.
Single-root direct saves (`ExecuteStatement`) are not affected.

```csharp
optionsBuilder.UseDynamo(options =>
{
    options.MaxTransactionSize(50);
});
```

Lower this when you intentionally want large saves to split earlier, usually to keep transaction
size predictable or to combine with `UseChunking` so overflow happens at a boundary you control.
Leave it at `100` when your main goal is maximizing the amount of work that can stay in one atomic
transaction.

`MaxTransactionSize` must be between 1 and 100 (inclusive). Values outside this range throw
`InvalidOperationException` during options configuration (or when setting per-context overrides).

### `MaxBatchWriteSize`

When EF Core's `AutoTransactionBehavior` is set to `Never`, multi-root saves use DynamoDB's
non-atomic `BatchExecuteStatement` API instead of `ExecuteTransaction`. `MaxBatchWriteSize` caps
the number of write operations per batch call (DynamoDB's hard maximum is 25):

```csharp
optionsBuilder.UseDynamo(options =>
{
    options.MaxBatchWriteSize(10);
});
```

This mainly matters when you have explicitly opted out of automatic transactions. Lower values can
reduce the blast radius of a failed non-atomic batch and make large writes easier to reason about,
at the cost of more round trips. Leave it at the default when throughput matters more than smaller
batch boundaries.

`MaxBatchWriteSize` must be between 1 and 25 (inclusive).

!!! warning "Batched multi-root saves require `acceptAllChangesOnSuccess=true`"

    With `AutoTransactionBehavior.Never`, multi-root batched saves are not supported for
    `SaveChangesAsync(false, ...)`. The provider throws `InvalidOperationException` before writes
    are sent.

### `DynamoAutomaticIndexSelectionMode`

By default the provider executes every query against the base table. Enable automatic index
selection to let the provider route compatible queries to a Global or Local Secondary Index:

| Mode            | Behavior                                                                                  |
| --------------- | ----------------------------------------------------------------------------------------- |
| `Off` (default) | No automatic routing — use explicit `.WithIndex("name")` hints                            |
| `SuggestOnly`   | Analyzes candidate indexes and emits `DYNAMO_IDX*` diagnostics; does not change the query |
| `Conservative`  | Automatically routes queries to an unambiguous matching index                             |

Use `Off` when you want query behavior to stay completely explicit in application code. Use
`SuggestOnly` while validating a schema or rolling the feature out, because it shows where an index
would help without changing production behavior. Use `Conservative` when you want safer automatic
routing for obvious matches, but still want the provider to avoid guessing between multiple
plausible indexes.

```csharp
optionsBuilder.UseDynamo(options =>
{
    options.UseAutomaticIndexSelection(DynamoAutomaticIndexSelectionMode.Conservative);
});
```

See [Index Selection](../querying/index-selection.md) for details on how the provider chooses
an index and what diagnostics it emits.

### `AutoTransactionBehavior` (EF Core standard)

`AutoTransactionBehavior` is a standard EF Core setting — not DynamoDB-specific — but it directly
affects which DynamoDB API the provider uses:

| Value                     | Single-root save behavior | Multi-root save behavior                                                                             |
| ------------------------- | ------------------------- | ---------------------------------------------------------------------------------------------------- |
| `WhenNeeded` (EF default) | Direct `ExecuteStatement` | `ExecuteTransaction` (or chunked `ExecuteTransaction` calls when overflow + `UseChunking`)           |
| `Always`                  | Direct `ExecuteStatement` | `ExecuteTransaction`; if root writes exceed `MaxTransactionSize`, throws `InvalidOperationException` |
| `Never`                   | Direct `ExecuteStatement` | Non-atomic `BatchExecuteStatement`; `MaxBatchWriteSize` applies                                      |

```csharp
context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
```

In practice, `WhenNeeded` is the balanced default for most apps because it preserves atomicity only
when the save shape requires it. Choose `Always` when you want stricter, more predictable
transaction usage for multi-root saves and prefer hard failures over fallback behavior. Choose
`Never` only when you explicitly want to trade atomicity for batch-style throughput.

## Per-Context Runtime Overrides

The `context.Database` facade lets you override transaction and batch settings for a specific
context instance, without changing the startup configuration. Overrides take effect immediately and
apply to all subsequent `SaveChangesAsync` calls on that instance.

Overrides are scoped to that `DbContext` instance only and are discarded when the instance is
disposed.

```csharp
// Override for this context instance only
context.Database.SetTransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking);
context.Database.SetMaxTransactionSize(25);
context.Database.SetMaxBatchWriteSize(10);

// Read the effective value (override if set, else startup option, else provider default)
var size = context.Database.GetMaxTransactionSize();
```

**Precedence (highest to lowest):**

1. Per-context override (`context.Database.Set...`)
1. Startup option (`UseDynamo(options => ...)`)
1. Provider default (`Throw`, `100`, `25`)

## See also

- [Client Setup](client-setup.md)
- [Table and Key Mapping](table-key-mapping.md)
- [Saving Data / Transactions](../saving/transactions.md)
- [Index Selection](../querying/index-selection.md)
