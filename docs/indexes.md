---
icon: lucide/table-properties
---

# Indexes

## Table keys vs EF keys

This provider treats DynamoDB table-key mapping as a Dynamo-specific concern.

- `HasKey(...)` configures the EF primary key only.
- `HasPartitionKey(...)` identifies the DynamoDB partition key.
- `HasSortKey(...)` identifies the DynamoDB sort key.
- Dynamo naming conventions (`PK` / `PartitionKey`, `SK` / `SortKey`) are also supported.

That means this model is **not** enough on its own:

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
- The optional GSI sort key is never inferred from `HasKey(...)` or EF key ordering.
- GSI key schema is part of the public configuration API because DynamoDB requires explicit index identity and key shape.

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
- `HasKey(...)` alone is not sufficient to satisfy those prerequisites.

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

- the query hint surface exists,
- metadata for GSIs/LSIs can be configured,
- but full index-aware PartiQL source selection is not wired yet.

So today, configuring an index documents intent and prepares the model surface. Calling
`WithIndex(...)` currently throws immediately until `FROM "Table"."Index"` generation is
implemented.

## Current limitations

- Query predicates do not automatically imply GSI/LSI selection.
- Full secondary-index query planning is still in progress.
- Projection-specific index behavior is intentionally deferred; the current workflow assumes full-entity compatibility.

See also:

- [Configuration](configuration.md)
- [Limitations](limitations.md)
- [Operators](operators.md)
