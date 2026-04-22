---
title: Table and Key Mapping
description: How to map entities to DynamoDB tables and configure partition and sort keys.
---

# Table and Key Mapping

_Each entity type maps to a DynamoDB table by name, with explicit configuration — or convention-based
discovery — for which properties serve as the partition key and optional sort key._

## Mapping to a Table

Use `ToTable` inside `OnModelCreating` to specify which DynamoDB table an entity type maps to:

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasPartitionKey(x => x.CustomerId);
});
```

The table must already exist — the provider does not create or migrate DynamoDB tables.

If you omit `ToTable`, the provider falls back to the CLR type name (`Order` → `"Order"`). Pass
`null` to clear an explicit mapping and restore the convention:

```csharp
b.ToTable(null); // reverts to CLR type name
```

## Partition Key

Every DynamoDB table has a partition key. Declare which property maps to it using `HasPartitionKey`:

```csharp
// Lambda overload (recommended — compile-time safe)
b.HasPartitionKey(x => x.CustomerId);

// String overload
b.HasPartitionKey("CustomerId");
```

The provider automatically sets the EF Core primary key to match the partition key (or
`[partitionKey, sortKey]` when a sort key is also declared). **Do not call `HasKey` or apply the
`[Key]` attribute on root entity types** — the provider rejects those configurations and throws
during model finalization.

```csharp
// ✗ Not supported for root DynamoDB entities
b.HasKey(x => x.CustomerId);

// ✓ Correct
b.HasPartitionKey(x => x.CustomerId);
```

### Convention-based discovery

Properties named `PK` or `PartitionKey` (comparison is case-insensitive) are automatically
designated as the partition key — no explicit `HasPartitionKey` call is needed:

```csharp
public class Order
{
    public string PK { get; set; } = null!;   // discovered as partition key
    public string Description { get; set; } = null!;
}
```

Convention-based discovery only considers EF-mapped properties. Getter-only computed properties
(for example `public string Pk => $"order#{Id}";`) are typically not mapped by EF Core conventions
and cannot be used as table keys as-is. Use a materialized key member instead (a settable property
or a getter-only property backed by an EF-mapped field).

!!! warning "Ambiguous key names"

    If an entity type has both a `PK` property and a `PartitionKey` property (or both `SK` and
    `SortKey`) and no explicit override is configured, the provider throws `InvalidOperationException`
    during model finalization. Use `HasPartitionKey` to resolve the ambiguity.

## Sort Key

A sort key is optional. When present, it combines with the partition key to form a composite
primary key:

```csharp
b.HasPartitionKey(x => x.CustomerId);
b.HasSortKey(x => x.OrderId);
// EF primary key is automatically set to [CustomerId, OrderId]
```

String overload:

```csharp
b.HasSortKey("OrderId");
```

Convention-based discovery: properties named `SK` or `SortKey` (case-insensitive) are
automatically designated as the sort key.

Omit `HasSortKey` entirely for tables that have only a partition key.

## Key Property Requirements

Key properties must be real, materialized EF properties. The provider validates this during model
finalization and rejects:

- Getter-only computed properties with no setter or backing field (for example
    `public string Pk => $"order#{Id}";`)
- Shadow properties
- Runtime-only properties

**Supported patterns:**

Normal settable property:

```csharp
public string CustomerId { get; set; } = null!;
```

Private setter:

```csharp
public string CustomerId { get; private set; } = null!;
```

Getter-only with a backing field (works when the backing field is EF-mapped):

```csharp
private string _pk = null!;
public string Pk => _pk;

// Call from domain logic to update the key
private void RecomputeKeys() => _pk = $"customer#{ExternalId}";
```

## Model Validation

The provider validates the key configuration at model finalization and throws
`InvalidOperationException` for any of the following:

- Partition or sort key property does not exist on the entity type
- Partition or sort key is a shadow property or runtime-only property
- Root entity type has an explicit `HasKey(...)` or `[Key]` configured
- Sort key is declared but no resolvable partition key exists
- Multiple entity types share a DynamoDB table but disagree on the partition key or sort key
    attribute name

## See also

- [Attribute Naming](attribute-naming.md)
- [Entities and Keys](../modeling/entities-keys.md)
- [Secondary Indexes](../modeling/secondary-indexes.md)
