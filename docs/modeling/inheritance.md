---
title: Inheritance and Discriminators
description: How entity inheritance and discriminator columns work in the DynamoDB EF Core provider.
---

# Inheritance and Discriminators

_The provider uses a discriminator attribute stored on each DynamoDB item to distinguish between derived entity types within the same table._

## Table-Per-Hierarchy

Table-Per-Hierarchy (TPH) is the only supported inheritance strategy. All concrete types in a
hierarchy share one DynamoDB table. Table-Per-Type (TPT) and Table-Per-Concrete-Type (TPC) are
not supported.

Derived types inherit the table name and key configuration from their base type — you only
configure those on the root:

```csharp
modelBuilder.Entity<Person>(builder =>
{
    builder.ToTable("People");
    builder.HasPartitionKey(x => x.Pk);
    builder.HasSortKey(x => x.Sk);
});

modelBuilder.Entity<Employee>(builder =>
    builder.HasBaseType<Person>());

modelBuilder.Entity<Manager>(builder =>
{
    builder.HasBaseType<Person>();
    builder.Property(x => x.Level).HasAttributeName("managerLevel");
});
```

## Discriminator Configuration

### Default behavior

When two or more concrete types share a DynamoDB table, the provider automatically adds a
discriminator attribute at model finalization. The discriminator stores the entity type name so EF
Core can materialize the correct CLR type for each item.

Defaults:

- Attribute name: `$type`
- Attribute value per type: the entity type short name (e.g. `"Employee"`, `"Manager"`)

When only one concrete type maps to a table, the provider adds then removes the discriminator
during finalization — no `$type` attribute is written to DynamoDB for that entity.

### Changing the discriminator name

To change the discriminator attribute name across all shared-table groups in the context:

```csharp
modelBuilder.HasEmbeddedDiscriminatorName("$kind");
```

### Disabling the discriminator

To opt out of type-level filtering entirely:

```csharp
modelBuilder.Entity<User>(builder =>
{
    builder.ToTable("app-table");
    builder.HasPartitionKey(x => x.Pk);
    builder.HasSortKey(x => x.Sk);
    builder.HasNoDiscriminator();
});
```

Calling `HasNoDiscriminator()` on any entity in a shared-table group disables discrimination for
all entities in that group — you only need to call it once.

!!! warning "HasNoDiscriminator removes type-level query filtering"

    Without a discriminator, EF Core does not inject a type predicate on queries. All items in
    the table that match the key conditions are returned regardless of entity type. Only disable
    the discriminator when partition or sort key patterns already guarantee type isolation.

### Unrelated types sharing a table

The discriminator is not limited to class inheritance hierarchies. Any two entity types that map
to the same table — even with no `HasBaseType` relationship — automatically get a discriminator:

```csharp
modelBuilder.Entity<User>(builder =>
{
    builder.ToTable("app-table");
    builder.HasPartitionKey(x => x.Pk);
    builder.HasSortKey(x => x.Sk);
});

modelBuilder.Entity<Order>(builder =>
{
    builder.ToTable("app-table");
    builder.HasPartitionKey(x => x.Pk);
    builder.HasSortKey(x => x.Sk);
});
```

Items in `"app-table"` will have `$type = "User"` or `$type = "Order"` written automatically.

!!! note "Shared-table key schema must match"

    All entity types that map to the same table must resolve to identical partition key and sort
    key attribute names and key type categories. The provider validates this at model finalization.

## Querying Derived Types

The provider injects the discriminator predicate automatically — write queries against the entity
type as usual and the filter is added for you.

A base-type query includes all known concrete discriminator values:

```csharp
context.People.Where(x => x.Pk == "TENANT#1").ToListAsync();
```

```sql
SELECT "pk", "sk", "$type", "name", "department", "managerLevel"
FROM "People"
WHERE "pk" = 'TENANT#1'
  AND ("$type" = 'Employee' OR "$type" = 'Manager')
```

A derived-type query scopes the predicate to that type:

```csharp
context.Employees.Where(x => x.Pk == "TENANT#1").ToListAsync();
```

```sql
SELECT "pk", "sk", "$type", "name", "department"
FROM "People"
WHERE "pk" = 'TENANT#1' AND "$type" = 'Employee'
```

Base-type queries materialize the correct concrete CLR types polymorphically — a `DbSet<Person>`
query returns a mix of `Employee` and `Manager` instances. The `$type` attribute is always
included in the `SELECT` list when discrimination is active.

!!! tip "Index selection and hierarchy queries"

    When automatic index selection is enabled, base-type queries are only routed to indexes
    declared on the queried base type or its ancestors. Indexes declared only on a sibling derived
    type are never selected for base-type queries. See
    [Index Selection](../querying/index-selection.md).

## See also

- [Entities and Keys](entities-keys.md)
- [Filtering](../querying/filtering.md)
- [Table and Key Mapping](../configuration/table-key-mapping.md)
- [Index Selection](../querying/index-selection.md)
