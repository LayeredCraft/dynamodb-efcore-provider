---
title: Table lifecycle
---

# Table lifecycle

The provider never creates DynamoDB tables implicitly during `SaveChanges`. Provision tables
externally, or call `Database.EnsureCreatedAsync()` during application startup or tests.

```csharp
await using var db = new OrdersContext(options);
await db.Database.EnsureCreatedAsync();
```

`EnsureCreatedAsync` creates one physical DynamoDB table for each mapped table group in the EF
model. Created tables use DynamoDB on-demand billing (`PAY_PER_REQUEST`); provisioned throughput
configuration is not exposed yet.

By default, lifecycle APIs wait for DynamoDB management operations to finish before returning:
created or updated tables must become `ACTIVE`, all GSIs must become `ACTIVE`, and deleted tables
must stop appearing in `DescribeTable`. The wait uses exponential backoff and a per-operation
timeout. Configure it with provider options:

```csharp
options.UseDynamo(dynamo => dynamo.TableLifecycle(lifecycle =>
{
    lifecycle.InitialPollingDelay = TimeSpan.FromSeconds(1);
    lifecycle.MaxPollingDelay = TimeSpan.FromSeconds(5);
    lifecycle.BackoffMultiplier = 1.5;
    lifecycle.Timeout = TimeSpan.FromMinutes(10);
}));
```

Set `WaitForCompletion = false` to return after DynamoDB accepts create or delete requests without
waiting for the final table state transition to complete. `EnsureCreatedAsync` still waits when later
work requires an active table: indexed table creates wait until the table and indexes are `ACTIVE`,
missing GSI additions wait after each `UpdateTable` call, and newly created tables wait before
configured seeding runs.

## Existing tables

When a mapped table already exists, `EnsureCreatedAsync` validates the table key schema, key
attribute scalar types, and secondary-index key/projection shapes that the provider can infer from
EF metadata.

- Missing global secondary indexes are added with `UpdateTable` for on-demand (`PAY_PER_REQUEST`)
    tables. Existing provisioned tables must create missing GSIs externally because DynamoDB
    requires per-index throughput and provider lifecycle throughput configuration is not exposed
    yet. The method waits for the table and GSIs to become `ACTIVE` after each update because
    DynamoDB rejects overlapping secondary-index lifecycle operations.
- Local secondary indexes must exist when the table is created. Missing or mismatched LSIs cause an
    exception because DynamoDB cannot add LSIs to an existing table.
- `Include` secondary-index projection is not supported for lifecycle creation yet because the EF
    metadata does not track the non-key projected attribute list. Use `All` or `KeysOnly` projection.

## Deleting mapped tables

`Database.EnsureDeletedAsync()` deletes only tables mapped by the current EF model. It ignores
already-missing tables and returns whether any mapped table was deleted. When lifecycle waiting is
enabled, it polls `DescribeTable` until DynamoDB returns `ResourceNotFoundException` for each table.

## Synchronous APIs

DynamoDB lifecycle operations are async-only. `EnsureCreated`, `EnsureDeleted`, and `CanConnect`
throw `NotSupportedException`; use `EnsureCreatedAsync`, `EnsureDeletedAsync`, and
`CanConnectAsync`.

## Seeding

`HasData` model seed data is inserted only for entity types mapped to tables created by the current
`EnsureCreatedAsync` call. It does not reseed existing tables, and it does not run when
`EnsureCreatedAsync` only adds a missing GSI to an existing table.

`UseAsyncSeeding` runs on every `EnsureCreatedAsync` call. Its `created` flag is `true` when a table
or index was created, and `false` when no schema operation was needed. Because synchronous lifecycle
APIs are unsupported, configure `UseAsyncSeeding` instead of sync-only `UseSeeding`.

## AWS references

- [CreateTable](https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_CreateTable.html)
- [UpdateTable](https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_UpdateTable.html)
- [Secondary indexes](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/SecondaryIndexes.html)
- [Local secondary indexes](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/LSI.html)
