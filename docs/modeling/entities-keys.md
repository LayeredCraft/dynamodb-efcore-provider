---
title: Entities and Keys
description: How to define entities and configure DynamoDB partition and sort keys.
---

# Entities and Keys

_Every entity type in the DynamoDB EF Core provider must declare a partition key, and optionally a sort key, which together form the item's primary key in DynamoDB._

## Defining an Entity

Root entity types (non-owned, non-derived) map to DynamoDB tables and must resolve table keys.
Key mapping can be explicit (`HasPartitionKey(...)`, `HasSortKey(...)`) or convention-based.

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasPartitionKey(x => x.CustomerId);
    b.HasSortKey(x => x.OrderId); // optional
});
```

The provider derives the EF primary key from this mapping. For root entities, do not configure
`HasKey(...)` or `[Key]`.

If no explicit `ToTable(...)` is configured, the provider uses the CLR type name as the table
name.

## Defaults and Overrides

For root entity types, table/key mapping resolves in this order (highest precedence first):

1. Explicit configuration (`ToTable(...)`, `HasPartitionKey(...)`, `HasSortKey(...)`)
1. Conventions (`PK`/`PartitionKey`, `SK`/`SortKey`; table name from CLR type)
1. Validation outcome (missing partition key throws; partition key resolved with no sort key
    means partition-key-only)

`HasKey(...)` and `[Key]` are not DynamoDB key overrides for root entities.

## Partition Key

Every DynamoDB table has a partition key. Configure it with `HasPartitionKey(...)` or use
conventional property names (`PK` or `PartitionKey`, case-insensitive).

`HasPartitionKey(...)` overrides convention-based partition key discovery.

Partition keys must be mapped, non-nullable EF properties and resolve to DynamoDB key-supported
provider types (string, number, or binary).

## Sort Key

Sort keys are optional. When present, the table key shape is `[partitionKey, sortKey]`.

Configure with `HasSortKey(...)` or use conventional property names (`SK` or `SortKey`,
case-insensitive).

`HasSortKey(...)` overrides convention-based sort key discovery.

## Composite Keys

In DynamoDB, a composite table key means exactly two parts: partition key + sort key.

For root entities, explicit EF `HasKey(...)` configuration is not the source of truth for table
keys. The provider validates that the EF primary key shape matches the configured DynamoDB key
mapping and throws during model finalization when they diverge.

## See also

- [Table and Key Mapping](../configuration/table-key-mapping.md)
- [Owned Types](owned-types.md)
- [Secondary Indexes](secondary-indexes.md)
- [DynamoDB Concepts for EF Developers](../dynamodb-concepts.md)
