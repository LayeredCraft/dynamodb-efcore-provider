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

## Existing tables

When a mapped table already exists, `EnsureCreatedAsync` validates the table key schema and
secondary-index key/projection shapes that the provider can infer from EF metadata.

- Missing global secondary indexes are added with `UpdateTable` and the method waits until the
    table and GSIs are `ACTIVE`.
- Local secondary indexes must exist when the table is created. Missing or mismatched LSIs cause an
    exception because DynamoDB cannot add LSIs to an existing table.
- `Include` secondary-index projection is not supported for lifecycle creation yet because the EF
    metadata does not track the non-key projected attribute list. Use `All` or `KeysOnly` projection.

## Deleting mapped tables

`Database.EnsureDeletedAsync()` deletes only tables mapped by the current EF model. It ignores
already-missing tables and returns whether any mapped table was deleted.

## Synchronous APIs

DynamoDB lifecycle operations are async-only. `EnsureCreated`, `EnsureDeleted`, and `CanConnect`
throw `NotSupportedException`; use `EnsureCreatedAsync`, `EnsureDeletedAsync`, and
`CanConnectAsync`.

## Seeding

`HasData` model seed data is inserted only after at least one new table is created. It does not run
when `EnsureCreatedAsync` only adds a missing GSI to an existing table.

`UseAsyncSeeding` runs on every `EnsureCreatedAsync` call. Its `created` flag is `true` when a table
or index was created, and `false` when no schema operation was needed. Because synchronous lifecycle
APIs are unsupported, configure `UseAsyncSeeding` instead of sync-only `UseSeeding`.

## AWS references

- [CreateTable](https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_CreateTable.html)
- [UpdateTable](https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_UpdateTable.html)
- [Local secondary indexes](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/LSI.html)
