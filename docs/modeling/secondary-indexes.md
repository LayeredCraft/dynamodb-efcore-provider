---
title: Secondary Indexes
description: How to configure and query Global Secondary Indexes (GSI) and Local Secondary Indexes (LSI).
---

# Secondary Indexes

_Global Secondary Indexes (GSIs) and Local Secondary Indexes (LSIs) let you query DynamoDB on attributes other than the primary key; the provider exposes these as queryable index targets._

## Global Secondary Indexes (GSI)

GSIs are always declared explicitly — the provider never infers them. A GSI can have a partition
key only, or a partition key and a sort key.

Partition key only:

```csharp
modelBuilder.Entity<Order>(builder =>
{
    builder.ToTable("Orders");
    builder.HasPartitionKey(x => x.CustomerId);
    builder.HasSortKey(x => x.OrderId);

    builder.HasGlobalSecondaryIndex("ByStatus", x => x.Status);
});
```

Partition key and sort key:

```csharp
builder.HasGlobalSecondaryIndex("ByStatusCreatedAt", x => x.Status, x => x.CreatedAt);
```

Key rules:

- The GSI partition key is never inferred from the table key — the first argument to
    `HasGlobalSecondaryIndex` is always the GSI partition key.
- GSI key properties must resolve to DynamoDB key types: `string`, number, or `byte[]`.
- Nullable GSI key properties are allowed and map to sparse-index membership: items without a
    scalar value for the key attribute are not included in the index.
- If the CLR property name differs from the DynamoDB attribute name, set it with
    `HasAttributeName(...)` — GSI key resolution uses the final attribute name.

## Local Secondary Indexes (LSI)

LSIs reuse the table's partition key and define an alternate sort key. The table must have both a
partition key and sort key already configured.

```csharp
modelBuilder.Entity<Order>(builder =>
{
    builder.ToTable("Orders");
    builder.HasPartitionKey(x => x.CustomerId);
    builder.HasSortKey(x => x.OrderId);

    builder.HasLocalSecondaryIndex("ByCreatedAt", x => x.CreatedAt);
});
```

Convention-based table keys work the same way:

```csharp
public sealed class Order
{
    public string PK { get; set; } = null!;
    public string SK { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

modelBuilder.Entity<Order>(builder =>
{
    builder.ToTable("Orders");
    builder.HasLocalSecondaryIndex("ByCreatedAt", x => x.CreatedAt);
});
```

!!! warning "LSI requires a table sort key"

    Calling `HasLocalSecondaryIndex(...)` on an entity without a sort key throws at model
    finalization.

Additional rules:

- Nullable LSI alternate sort key properties are allowed (sparse index membership).
- The alternate sort key must resolve to a distinct DynamoDB attribute name from both the table
    partition key and sort key — the validator checks by attribute name, not just by property
    reference.

## Projection Types

Each secondary index has a projection type that determines which attributes DynamoDB stores in
the index. The `DynamoSecondaryIndexProjectionType` enum has three values:

| Value               | Behavior                                                                 |
| ------------------- | ------------------------------------------------------------------------ |
| `All` **(default)** | All item attributes are projected — supports full entity materialization |
| `KeysOnly`          | Only table and index key attributes are projected                        |
| `Include`           | A configured subset of attributes is projected                           |

The default when calling `HasGlobalSecondaryIndex` or `HasLocalSecondaryIndex` is `All`.

!!! warning "Non-All projection indexes cannot materialize full entities"

    Querying a `KeysOnly` or `Include` projection index via `.WithIndex(...)` and materializing a
    full entity will fail at runtime when required attributes are absent from the response. The
    provider does not silently default or null missing properties.

!!! note "Partial projection query support"

    Automatic index selection only considers `All`-projection indexes.

    `KeysOnly` and `Include` indexes can still be targeted explicitly with `.WithIndex(...)`, but
    queries must only read attributes projected by that index. Materializing full entities from
    non-`All` projection indexes can fail at runtime when required attributes are missing.

## Using Indexes in Queries

Use `.WithIndex("name")` to target a named secondary index explicitly. The provider emits
`FROM "Table"."Index"` in the PartiQL statement.

```csharp
var orders = await context.Orders
    .WithIndex("ByStatusCreatedAt")
    .Where(x => x.Status == "pending")
    .ToListAsync();
```

Use `.WithoutIndex()` to suppress index selection and force execution on the base table. The
provider emits diagnostic `DYNAMO_IDX006` when index selection is suppressed.

```csharp
var orders = await context.Orders
    .WithoutIndex()
    .Where(x => x.CustomerId == customerId)
    .ToListAsync();
```

!!! warning "WithIndex and WithoutIndex cannot be combined"

    Calling both on the same query throws `InvalidOperationException` at translation time.

When automatic index selection is enabled (see [DbContext Options](../configuration/dbcontext.md)),
the provider routes compatible queries to an index automatically. Automatic selection is
conservative:

- it requires the query's filter to cover the index partition key,
- it only considers `All`-projection indexes,
- it falls back to the base table on ties or unsupported query shapes,
- it only considers indexes declared on the queried entity type or its base types — base-type
    queries are never routed to subtype-only sparse indexes.

See [Index Selection](../querying/index-selection.md) for full details on the routing algorithm.

## See also

- [Index Selection](../querying/index-selection.md)
- [Entities and Keys](entities-keys.md)
- [DbContext Options](../configuration/dbcontext.md)
- [Attribute Naming](../configuration/attribute-naming.md)
- [Limitations](../limitations.md)
