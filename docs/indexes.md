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

var baseTableOnly = context.Orders
    .WithoutIndex()
    .Where(x => x.CustomerId == customerId);
```

Current scope:

- `WithIndex(...)` validates the named index against the queried entity type and emits
    `FROM "Table"."Index"`,
- `WithoutIndex()` suppresses both automatic and explicit index selection for the query and keeps
    execution on the base table,
- metadata for GSIs/LSIs can be configured,
- automatic index selection can route compatible queries when `UseAutomaticIndexSelection(...)` is enabled.

Guardrails:

- `.WithIndex(...)` and `.WithoutIndex()` cannot be combined on the same query; translation throws
    `InvalidOperationException`.
- `.WithoutIndex()` emits informational diagnostic `DYNAMO_IDX006` when index selection is
    suppressed.

Automatic selection is conservative:

- it requires partition-key coverage,
- it only considers `ALL` projection indexes,
- it falls back to the base table on ties or unsupported shapes,
- it only considers indexes declared on the queried entity type or its base types, so base-type
    queries are not routed onto subtype-only sparse indexes.

## Projection scope (v1)

The provider treats secondary indexes as full-entity compatible only. All secondary indexes must
use `ALL` projection to be eligible for entity materialization:

- Only `ALL`-projection indexes are considered for automatic index selection.
- Querying a `KEYS_ONLY` or `INCLUDE`-projection index via `.WithIndex(...)` is not blocked at
    configuration time, but entity materialization will fail at runtime if required attributes are
    absent from the index.
- The provider does not auto-null or default non-projected properties; missing required attributes
    cause a materialization error.

Partial-projection support (read-model patterns, `KEYS_ONLY`/`INCLUDE` index queries, fetch-back)
is deferred to a future milestone.

## Current limitations

- Query predicates do not guarantee index selection; unsupported shapes and ties fall back to the
    base table.
- Only `ALL`-projection indexes are eligible for entity materialization. Querying a non-`ALL` index
    and materializing a full entity will throw if required attributes are absent; the provider does
    not silently default missing properties.
- Partial projection support (`KEYS_ONLY`, `INCLUDE`) is deferred to a future milestone.

See also:

- [Configuration](configuration.md)
- [Limitations](limitations.md)
- [Operators](operators.md)
