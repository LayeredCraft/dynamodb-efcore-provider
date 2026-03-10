---
icon: lucide/table-properties
---

# Indexes

## Table keys vs EF keys

This provider treats DynamoDB table-key mapping as a Dynamo-specific concern.

- `HasPartitionKey(...)` identifies the DynamoDB partition key.
- `HasSortKey(...)` identifies the DynamoDB sort key.
- The provider derives the EF primary key automatically from that mapping.
- Root entities should not call `HasKey(...)` directly.
- Dynamo naming conventions (`PK` / `PartitionKey`, `SK` / `SortKey`) are also supported.

That means this model is invalid for a root DynamoDB entity:

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasKey(x => new { x.CustomerId, x.OrderId });
});
```

Use Dynamo-specific key mapping instead:

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasPartitionKey(x => x.CustomerId);
    b.HasSortKey(x => x.OrderId);
});
```

## Global secondary indexes (GSI)

GSIs are always configured explicitly.

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasPartitionKey(x => x.CustomerId);
    b.HasSortKey(x => x.OrderId);

    b.HasGlobalSecondaryIndex("ByStatus", x => x.Status);
    b.HasGlobalSecondaryIndex("ByCustomerCreatedAt", x => x.CustomerId, x => x.CreatedAtUtc);
});
```

Notes:

- The GSI partition key is never inferred from the table key.
- The optional GSI sort key is never inferred from EF key ordering.
- GSI key schema is part of the public configuration API because DynamoDB requires explicit index identity and key shape.
- GSI key properties must resolve to DynamoDB key-compatible provider types (`string`, number, or `byte[]`).
- Nullable GSI key properties are allowed and map to sparse-index membership semantics (items without key-compatible scalar key attributes are not present in the index).

## Local secondary indexes (LSI)

LSIs reuse the table partition key and define a different sort key.

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasPartitionKey(x => x.CustomerId);
    b.HasSortKey(x => x.OrderId);

    b.HasLocalSecondaryIndex("ByCreatedAt", x => x.CreatedAtUtc);
});
```

Requirements:

- The table must already resolve a DynamoDB partition key.
- The table must already resolve a DynamoDB sort key.
- Root entities must resolve the table keys through `HasPartitionKey(...)` / `HasSortKey(...)` or the Dynamo naming conventions.
- The LSI alternate sort key property must resolve to a DynamoDB key-compatible provider type (`string`, number, or `byte[]`).
- Nullable LSI alternate sort key properties are allowed and result in sparse index membership for items without key-compatible scalar alternate sort key attributes.

Convention-based table keys also work:

```csharp
public sealed class Order
{
    public string PK { get; set; } = null!;
    public string SK { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}

modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasLocalSecondaryIndex("ByCreatedAt", x => x.CreatedAtUtc);
});
```

## Query-time hint API

The provider also exposes an explicit query hint API:

```csharp
var query = context.Orders
    .WithIndex("ByCustomerCreatedAt")
    .Where(x => x.CustomerId == customerId);
```

Current scope:

- `WithIndex(...)` validates the named index against the queried entity type and emits
  `FROM "Table"."Index"`,
- metadata for GSIs/LSIs can be configured,
- automatic index selection can route compatible queries when `UseAutomaticIndexSelection(...)` is enabled.

Automatic selection is conservative:

- it requires partition-key coverage,
- it only considers `ALL` projection indexes,
- it falls back to the base table on ties or unsupported shapes,
- it only considers indexes declared on the queried entity type or its base types, so base-type
  queries are not routed onto subtype-only sparse indexes.

## Current limitations

- Query predicates do not guarantee index selection; unsupported shapes and ties fall back to the
  base table.
- Projection-specific index behavior is intentionally deferred; the current workflow assumes full-entity compatibility.

See also:

- [Configuration](configuration.md)
- [Limitations](limitations.md)
- [Operators](operators.md)
