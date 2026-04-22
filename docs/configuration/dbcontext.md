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
    options.UseDynamo(o => o.DynamoDbClient(dynamoClient)));
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

### Transaction overflow

DynamoDB's `ExecuteTransaction` API accepts at most 100 items per call. When a single
`SaveChangesAsync` produces more write operations than `MaxTransactionSize`, the provider applies
the configured `TransactionOverflowBehavior`:

| Value             | Behavior                                                       |
| ----------------- | -------------------------------------------------------------- |
| `Throw` (default) | Throws `InvalidOperationException` before any writes are sent  |
| `UseChunking`     | Splits the write unit into multiple `ExecuteTransaction` calls |

```csharp
optionsBuilder.UseDynamo(options =>
{
    options.TransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking);
    options.MaxTransactionSize(50); // cap each transaction at 50 items; default is 100
});
```

!!! warning "Chunking is not globally atomic"

    Each chunk is individually atomic, but the overall `SaveChangesAsync` call is not. If chunk _N_
    fails, chunks _0..N-1_ are permanently committed and cannot be rolled back. Always re-query the
    affected entities before retrying a chunked save.

`MaxTransactionSize` must be between 1 and 100 (inclusive). Values outside this range throw
`InvalidOperationException` at startup.

### Batch write size

When EF Core's `AutoTransactionBehavior` is set to `Never`, multi-root saves use DynamoDB's
non-atomic `BatchExecuteStatement` API instead of `ExecuteTransaction`. `MaxBatchWriteSize` caps
the number of write operations per batch call (DynamoDB's hard maximum is 25):

```csharp
optionsBuilder.UseDynamo(options =>
    options.MaxBatchWriteSize(10));
```

`MaxBatchWriteSize` must be between 1 and 25 (inclusive).

### Automatic index selection

By default the provider executes every query against the base table. Enable automatic index
selection to let the provider route compatible queries to a Global or Local Secondary Index:

| Mode            | Behavior                                                                                  |
| --------------- | ----------------------------------------------------------------------------------------- |
| `Off` (default) | No automatic routing — use explicit `.WithIndex("name")` hints                            |
| `SuggestOnly`   | Analyzes candidate indexes and emits `DYNAMO_IDX*` diagnostics; does not change the query |
| `Conservative`  | Automatically routes queries to an unambiguous matching index                             |

```csharp
optionsBuilder.UseDynamo(options =>
    options.UseAutomaticIndexSelection(DynamoAutomaticIndexSelectionMode.Conservative));
```

See [Index Selection](../querying/index-selection.md) for details on how the provider chooses
an index and what diagnostics it emits.

### AutoTransactionBehavior (EF Core standard)

`AutoTransactionBehavior` is a standard EF Core setting — not DynamoDB-specific — but it directly
affects which DynamoDB API the provider uses for multi-root saves:

| Value                     | Multi-root save behavior                                                                                                                     |
| ------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| `WhenNeeded` (EF default) | Single-root: direct `ExecuteStatement`. Multi-root: `ExecuteTransaction`                                                                     |
| `Always`                  | Always uses `ExecuteTransaction` (single-root saves still go direct — wrapping a single write in a transaction adds latency with no benefit) |
| `Never`                   | Non-atomic `BatchExecuteStatement`; `MaxBatchWriteSize` applies                                                                              |

```csharp
context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
```

## Per-Context Runtime Overrides

The `context.Database` facade lets you override transaction and batch settings for a specific
context instance, without changing the startup configuration. Overrides take effect immediately and
apply to all subsequent `SaveChangesAsync` calls on that instance.

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
