---
title: Single-Table Design and Discriminators
description: How to model multiple entity types in one DynamoDB table and how discriminator behavior works in the provider.
---

# Single-Table Design and Discriminators

_Single-table design is the primary DynamoDB modeling pattern: store multiple entity types in one table, shape keys for your access patterns, and use discriminators so the provider materializes and filters the right CLR types._

## What this page covers

This page explains how these pieces work together:

- Single-table mapping in the EF model
- Partition/sort key design for mixed entity types
- Discriminator defaults and overrides
- Query behavior for base and derived entity types

If you are new to DynamoDB modeling, read [DynamoDB Concepts for EF Developers](../dynamodb-concepts.md) first.

## Single-table design in this provider

In this provider, single-table design means **multiple root entity types map to the same table name**.

In this page, _root entity type_ means a non-owned, non-derived entity configured directly on the
model.

That can be:

- A class hierarchy (`Person`, `Employee`, `Manager`)
- Unrelated types (`User`, `Order`, `Invoice`) that share one table

When two or more concrete types share a table, the provider treats them as one shared-table group.

### Why this matters

DynamoDB tables are schemaless beyond PK/SK, so mixed item shapes are normal. EF Core still needs
type information to materialize CLR objects correctly and keep queries type-safe. The discriminator
attribute is how that type boundary is enforced when needed.

### Shared-table key schema rules

All entity types sharing one table must agree on table key schema:

- Same partition key attribute name
- Same sort key attribute name (or all PK-only)
- Same key type category for each key attribute (string/number/binary)

The provider validates this at model finalization and throws if mappings diverge. See
[Table and Key Mapping](../configuration/table-key-mapping.md).

### Access-pattern-first contract

For production single-table models, treat PK/SK patterns as a contract:

- Start from query shapes you must support
- Encode those shapes into deterministic PK/SK value formats
- Map entity types to those formats, then use discriminators as a type-safety layer

If key values are inconsistent, discriminator filtering cannot fix the underlying access-pattern
drift.

## Model a shared table

### Example: unrelated types in one table

```csharp
modelBuilder.Entity<User>(b =>
{
    b.ToTable("app-table");
    b.HasPartitionKey(x => x.Pk);
    b.HasSortKey(x => x.Sk);
});

modelBuilder.Entity<Order>(b =>
{
    b.ToTable("app-table");
    b.HasPartitionKey(x => x.Pk);
    b.HasSortKey(x => x.Sk);
});
```

### Example: inheritance hierarchy in one table

TPH (table-per-hierarchy) is the only supported inheritance strategy.

```csharp
modelBuilder.Entity<Person>(b =>
{
    b.ToTable("People");
    b.HasPartitionKey(x => x.Pk);
    b.HasSortKey(x => x.Sk);
});

modelBuilder.Entity<Employee>(b => b.HasBaseType<Person>());

modelBuilder.Entity<Manager>(b =>
{
    b.HasBaseType<Person>();
    b.Property(x => x.Level).HasAttributeName("managerLevel");
});
```

Table-per-type (TPT) and table-per-concrete-type (TPC) are not supported.

### Implementation template

Use this sequence when introducing a new entity type into a shared table:

1. Define the item family and sort-key prefix (for example `ORDER#`, `INVOICE#`, `USER#METADATA`)
1. Define uniqueness at `(PK, SK)` for that family
1. Add entity mapping to the shared `ToTable(...)` target with the same PK/SK attribute names
1. Add query paths that include PK and the intended SK condition (`=`, `begins_with`, range)
1. Keep discriminator enabled unless keys alone guarantee strict type isolation

## Discriminator behavior

### Default behavior

When a shared-table group contains two or more concrete types, the provider configures a
discriminator automatically.

Defaults:

- Attribute name: `$type`
- Attribute value per type: entity short name (for example `"User"`, `"Order"`, `"Manager"`)

When a shared-table group resolves to exactly one concrete type, a discriminator is not persisted
for that group.

### Change discriminator attribute name

Use a model-level override:

```csharp
modelBuilder.HasEmbeddedDiscriminatorName("$kind");
```

### Disable discriminators for a shared table

If your key design already guarantees type isolation, you can disable discriminator filtering:

```csharp
modelBuilder.Entity<User>(b =>
{
    b.ToTable("app-table");
    b.HasPartitionKey(x => x.Pk);
    b.HasSortKey(x => x.Sk);
    b.HasNoDiscriminator();
});
```

Calling `HasNoDiscriminator()` on any root entity in a shared-table group disables discrimination
for the entire group.

!!! warning "Disabling discriminators removes type predicates"

    Without a discriminator, the provider does not inject type-level query filtering.
    Queries return all items matching key conditions, regardless of CLR type.
    Only use this when your PK/SK patterns guarantee type separation.

## Query behavior

The provider injects discriminator predicates automatically when discrimination is active.

### Runtime flow

At query time, these pieces combine in order:

1. DynamoDB key conditions decide which items are read (PK and optional SK conditions)
1. Provider discriminator predicate narrows items to allowed CLR types in the shared-table group
1. Materialization uses discriminator values to create the correct concrete CLR type

This is why key design and discriminator design should be treated as one system, not separate
features.

### Base-type query

Base queries include all concrete discriminator values in the group:

```csharp
context.People.Where(x => x.Pk == "TENANT#1").ToListAsync();
```

```sql
SELECT "pk", "sk", "$type", "name", "department", "managerLevel"
FROM "People"
WHERE "pk" = 'TENANT#1'
  AND ("$type" = 'Employee' OR "$type" = 'Manager')
```

### Derived-type query

Derived queries scope to one discriminator value:

```csharp
context.Employees.Where(x => x.Pk == "TENANT#1").ToListAsync();
```

```sql
SELECT "pk", "sk", "$type", "name", "department"
FROM "People"
WHERE "pk" = 'TENANT#1' AND "$type" = 'Employee'
```

!!! danger "Type-only narrowing is not a key-condition optimization"

    The discriminator predicate (`$type = ...`) is a filter, not a partition/sort key condition.
    DynamoDB applies that filter after reading matching key-range items.
    AWS documents that `Query` consumes the same read capacity whether a filter expression is
    present or not.

    In practice, PK + discriminator-only queries can consume more read units and add latency,
    because DynamoDB reads a broader item set first and then discards non-matching types.
    The read-cost term is:

    - **Provisioned mode**: Read Capacity Units (RCUs)
    - **On-demand mode**: Read Request Units (RRUs)

    Prefer adding a sort-key predicate (for example, `StartsWith`) that matches your item pattern.
    In this provider, `string.StartsWith(string)` is translated to DynamoDB `begins_with(...)`.
    See [Supported Operators](../querying/operators.md) for translation details.

    Safer access-pattern query (PK + SK prefix + discriminator):

    ```csharp
    context.Employees
        .Where(x => x.Pk == "TENANT#1" && x.Sk.StartsWith("EMPLOYEE#"))
        .ToListAsync();
    ```

    ```sql
    SELECT "pk", "sk", "$type", "name", "department"
    FROM "People"
    WHERE "pk" = 'TENANT#1'
      AND begins_with("sk", 'EMPLOYEE#')
      AND "$type" = 'Employee'
    ```

    This query shape aligns with single-table key design: PK selects the item collection, SK prefix
    narrows to the item family, and discriminator preserves EF type safety.
    For key-shape examples, continue with [Practical single-table pattern](#practical-single-table-pattern).

Base queries materialize polymorphically (`DbSet<Person>` can return `Employee` and `Manager`).
When discrimination is active, the discriminator attribute is included in projection.

### `OfType<TDerived>()` limitation

`Queryable.OfType<TDerived>()` is not currently translated by the provider.
Query derived sets directly (for example `context.Employees`) instead.

!!! tip "Index selection with inheritance/shared-table queries"

    With automatic index selection enabled, base-type queries are only routed to indexes declared
    on the queried base type (or its ancestors). Indexes declared only on sibling derived types are
    not chosen for base-type queries. See [Index Selection](../querying/index-selection.md).

## Practical single-table pattern

Use a shared key layout that encodes relationship and item shape in key values.

Example item collection for one tenant:

```text
PK         SK                   Item kind
---------- -------------------- -----------------
TENANT#1   USER#42              User profile
TENANT#1   ORDER#2026-04-01#17  Order
TENANT#1   ORDER#2026-04-02#18  Order
TENANT#1   INVOICE#18           Invoice
```

- Querying by `PK = TENANT#1` returns the tenant item collection.
- `begins_with(SK, 'ORDER#')` narrows to orders.
- Discriminator keeps EF type materialization/filtering correct when multiple CLR types are mapped.

Effective query shapes for this pattern usually look like:

- `PK = TENANT#1 AND begins_with(SK, 'ORDER#')`
- `PK = TENANT#1 AND SK = USER#42`
- `PK = TENANT#1 AND SK = USER#42#METADATA`

If a query shape always targets one SK prefix and cannot overlap other types, discriminator
filtering may be redundant. Keep it enabled by default unless you have verified isolation.

## How the pieces work together

Think about single-table modeling as three layered constraints:

1. **Key constraints (PK/SK)** define where an item lives and whether `(PK, SK)` is unique
1. **Type constraints (discriminator)** define which CLR type an item belongs to
1. **Query constraints (LINQ shape)** define which subset of that data you read for one use case

If any layer is weak, results can be broader than intended.

### Example: user + order + metadata in one partition

Assume these items share one tenant partition:

```text
PK         SK                   $type
---------- -------------------- --------
TENANT#1   USER#42              User
TENANT#1   USER#42#METADATA     UserMetadata
TENANT#1   ORDER#2026-04-01#17  Order
```

- `PK` groups related tenant data in one collection.
- `SK` pattern defines logical item kind and uniqueness inside that partition.
- `$type` lets EF materialize/query `User`, `UserMetadata`, and `Order` safely from one table.

That is the intended production pattern: key shape controls locality and queryability; discriminator
controls type-safe reads from mixed rows.

## Designing keys and discriminators together

Use key patterns for data locality and access patterns; use the discriminator for type safety and
materialization.

- **Partition key** groups related items you read together
- **Sort key** distinguishes item shapes and supports range/prefix queries
- **Discriminator** distinguishes CLR types in shared-table EF queries

In practice, most production models use all three together.

## Key semantics and data constraints

Single-table design works best when PK/SK values are treated as part of your domain contract, not
just storage fields.

### Uniqueness rules

- For PK-only tables: `PK` is unique per item.
- For PK+SK tables (most single-table designs): uniqueness is the **combination** of `PK` and `SK`.
- Multiple items should intentionally share the same `PK` when they belong to one item collection.

This means `PK` usually represents a grouping boundary (tenant, account, aggregate root), while
`SK` distinguishes each item shape within that group.

### Common key-value conventions

Use stable, prefixed key values so item intent is obvious and queryable:

- `PK = TENANT#1`, `SK = USER#42`
- `PK = TENANT#1`, `SK = ORDER#2026-04-01#17`
- `PK = TENANT#1`, `SK = USER#42#METADATA`

Centralize key construction so every writer uses the same format:

```csharp
public static class Keys
{
    public static string TenantPk(string tenantId) => $"TENANT#{tenantId}";
    public static string UserSk(string userId) => $"USER#{userId}";
    public static string UserMetadataSk(string userId) => $"USER#{userId}#METADATA";
    public static string OrderSk(DateOnly date, string orderId) => $"ORDER#{date:yyyy-MM-dd}#{orderId}";
}
```

Why prefixes help:

- Enforce predictable uniqueness boundaries
- Support targeted range queries (`begins_with(SK, 'ORDER#')`)
- Make mixed-type partitions readable and debuggable

If writers construct keys ad hoc in different services, prefix drift and query misses are common.

### Encode invariants in key patterns

Before finalizing keys, define invariants explicitly:

- What items must be unique per partition?
- What items may repeat under different prefixes?
- Which query paths rely on sort-key prefix filtering?

Treat these as model-level expectations. If the application violates them, you can read wrong
item shapes even when the EF model itself is valid.

### Keep or disable discriminator?

- Keep enabled (recommended): when shared partitions can contain multiple CLR types for the same
    query path
- Consider disabling: only when PK/SK patterns guarantee mutually exclusive type reads and you
    deliberately want no type predicate injection
- Avoid disabling early: it removes a safety boundary that prevents accidental cross-type reads

## Common mistakes to avoid

- Querying shared partitions with only `PK` (or PK + discriminator) when an SK predicate should be
    part of the access pattern
- Letting multiple writers generate inconsistent SK formats for the same item family
- Using ambiguous prefixes that overlap across types (`USER#` vs `USER_METADATA#` without a clear
    convention)
- Disabling discriminator filtering before key invariants and query shapes are validated in real
    traffic

## Recommended workflow

1. Start from access patterns (what must be fetched together)
1. Design PK/SK values to satisfy those patterns
1. Map all participating entity types to one table
1. Keep discriminators enabled unless key design alone guarantees safe isolation
1. Validate query shapes on base and derived sets
1. Inspect generated query text/logs to confirm key-focused predicates (`PK`, `SK`, `begins_with`)

## See also

- [DynamoDB Concepts for EF Developers](../dynamodb-concepts.md)
- [Entities and Keys](entities-keys.md)
- [Table and Key Mapping](../configuration/table-key-mapping.md)
- [Filtering](../querying/filtering.md)
- [Index Selection](../querying/index-selection.md)
