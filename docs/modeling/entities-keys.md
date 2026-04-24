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

!!! warning "Do not use HasKey or [Key]"

    `HasKey(...)` and `[Key]` are not respected by this provider for DynamoDB key mapping.
    Configure keys with `HasPartitionKey(...)` and optional `HasSortKey(...)` instead.

If no explicit `ToTable(...)` is configured, the provider uses the CLR type name as the table
name.

## Defaults and Overrides

For root entity types, table/key mapping resolves in this order (highest precedence first):

1. Explicit configuration (`ToTable(...)`, `HasPartitionKey(...)`, `HasSortKey(...)`)
1. Conventions (`PK`/`PartitionKey`, `SK`/`SortKey`; table name from CLR type)
1. Validation outcome (missing partition key throws; partition key resolved with no sort key
    means partition-key-only)

`HasKey(...)` and `[Key]` are not DynamoDB key overrides.

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

!!! warning "Composite keys must use partition/sort mapping"

    `HasKey(...)` and `[Key]` are not the source of truth for composite DynamoDB keys.
    Configure composite keys with `HasPartitionKey(...)` + `HasSortKey(...)`. If an EF primary key
    shape diverges from the configured DynamoDB key mapping, model finalization throws.

When a sort key is configured (explicitly or by convention), the provider derives the EF primary
key as `[partitionKey, sortKey]` in that order.

Explicit composite-key mapping:

```csharp
modelBuilder.Entity<Order>(b =>
    b.ToTable("Orders");
    b.HasPartitionKey(x => x.CustomerId);
    b.HasSortKey(x => x.OrderId);
});
```

Convention-based composite-key mapping (no explicit key calls):

```csharp
public sealed class Order
{
    public string Pk { get; set; } = null!;
    public string Sk { get; set; } = null!;
    public string Description { get; set; } = null!;
}
```

!!! note "Common validation failures"

    - Configuring `HasKey(...)` or `[Key]` on the entity
    - Declaring a sort key without a resolvable partition key
    - Ambiguous conventional names (both `PK` and `PartitionKey`, or both `SK` and `SortKey`)

See [Table and Key Mapping](../configuration/table-key-mapping.md) for full validation rules,
key-property requirements, and advanced mapping patterns.

## See also

- [Table and Key Mapping](../configuration/table-key-mapping.md)
- [Owned Types](owned-types.md)
- [Secondary Indexes](secondary-indexes.md)
- [DynamoDB Concepts for EF Developers](../dynamodb-concepts.md)
