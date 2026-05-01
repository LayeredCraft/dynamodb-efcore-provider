---
title: Table and Key Mapping
description: How to map entities to DynamoDB tables and configure partition and sort keys.
---

# Table and Key Mapping

_Each entity type maps to a DynamoDB table by name, with explicit configuration — or convention-based
discovery — for which properties serve as the partition key and optional sort key._

In this page, **root entity type** means a non-derived entity mapped directly to a DynamoDB table.
Complex properties are embedded in their owning entity's item and do not configure their own table
keys. See [Complex Properties and Collections](../modeling/complex-types.md).

## Defaults and Overrides

For root entity types, table and key mapping resolves in this order (highest precedence first):

1. Explicit configuration (`ToTable(...)`, `HasPartitionKey(...)`, `HasSortKey(...)`)
1. Conventions:
    - Table name: CLR type name
    - Partition key property name: `PK` or `PartitionKey` (case-insensitive)
    - Sort key property name: `SK` or `SortKey` (case-insensitive)
    - No fallback to EF `Id`/`[Key]` conventions for DynamoDB table keys
1. Validation outcome:
    - No resolvable partition key: model validation throws `InvalidOperationException`
    - Partition key resolved but no sort key configured/discovered: table is treated as
        partition-key-only

`HasKey(...)` and `[Key]` are not DynamoDB table-key overrides for root entities and are rejected.

## Mapping to a Table

Use `ToTable` inside `OnModelCreating` to specify which DynamoDB table an entity type maps to:

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasPartitionKey(x => x.CustomerId);
});
```

`ToTable(...)` is an explicit override of the default table-name convention.

The table must already exist — the provider does not create or migrate DynamoDB tables.

If no explicit `ToTable(...)` mapping is configured, the provider uses the CLR type name (for example,
`Order` -> `"Order"`).

If you need to clear a previously configured table name, call:

```csharp
b.ToTable(null);
```

## Partition Key

Every DynamoDB table has a partition key.

Use `HasPartitionKey(...)` to map it explicitly:

```csharp
// Lambda overload (recommended — compile-time safe)
b.HasPartitionKey(x => x.CustomerId);

// String overload
b.HasPartitionKey("CustomerId");
```

`HasPartitionKey(...)` is an explicit override of convention-based partition key discovery. If
both explicit and conventional candidates exist, the explicit mapping wins.

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

### Convention-based discovery (when no explicit override is configured)

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
(for example `public string Pk => $"order#{Id}";`) do not provide a persisted key storage member
and are not a good table-key pattern as-is. Use a materialized key member instead (a settable
property or a getter-only property backed by an EF-mapped field).

!!! warning "Ambiguous key names"

    If an entity type has both a `PK` property and a `PartitionKey` property (or both `SK` and
    `SortKey`) and no explicit override is configured, the provider throws `InvalidOperationException`
    during model finalization. Use `HasPartitionKey(...)` and/or `HasSortKey(...)` to resolve the
    ambiguity.

## Sort Key

A sort key is optional. When present, it combines with the partition key to form a composite
primary key:

`HasSortKey(...)` is an explicit override of convention-based sort key discovery.
If both explicit and conventional candidates exist, the explicit mapping wins.

```csharp
b.HasPartitionKey(x => x.CustomerId);
b.HasSortKey(x => x.OrderId);
// EF primary key is automatically set to [CustomerId, OrderId]
```

String overload:

```csharp
b.HasSortKey("OrderId");
```

Convention-based discovery (when no explicit override is configured): properties named `SK` or
`SortKey` (case-insensitive) are automatically designated as the sort key.

Omit `HasSortKey` entirely for tables that have only a partition key.

## Property Names vs Attribute Names

`HasPartitionKey(...)` and `HasSortKey(...)` select EF properties, not raw DynamoDB attribute
strings.

The final DynamoDB key attribute names still come from attribute naming configuration (for example
`HasAttributeName(...)` or an entity-level naming convention).

This is especially important when multiple entity types share a table: they must resolve to the
same partition/sort key attribute names.

## Key Property Requirements

Key properties must be real, materialized EF properties. The provider validates this during model
finalization and rejects:

- Shadow properties
- Runtime-only properties

Pure getter-only computed members (for example `public string Pk => $"order#{Id}";`) are not a
good key-member shape because they have no persisted key storage member of their own. Prefer a
settable key property or a getter-only key property backed by an EF-mapped field.

Key properties must also satisfy DynamoDB key-shape constraints:

- Required/non-nullable in EF metadata
- Effective provider type resolves to DynamoDB key-compatible type: string, number, or binary
    (`byte[]`)

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

// EF usually discovers _pk as the backing field by convention.
// If your field/property naming differs, configure it explicitly:
modelBuilder.Entity<Order>()
    .Property(x => x.Pk)
    .HasField("_pk");
```

## Model Validation

The provider validates the key configuration at model finalization and throws
`InvalidOperationException` for common failures such as:

- Partition or sort key property does not exist on the entity type
- Partition or sort key is a shadow property or runtime-only property
- Root entity type has an explicit `HasKey(...)` or `[Key]` configured
- Sort key is declared but no resolvable partition key exists
- Multiple entity types share a DynamoDB table but disagree on key schema, including:
    - partition key or sort key attribute name
    - key shape (PK-only vs PK+SK)
    - key type category for the same key attribute (string/number/binary)

Additional checks also validate key type compatibility and nullability.

## See also

- [Attribute Naming](attribute-naming.md)
- [Entities and Keys](../modeling/entities-keys.md)
- [Secondary Indexes](../modeling/secondary-indexes.md)
