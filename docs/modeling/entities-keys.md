---
title: Entities and Keys
description: How to define entities and configure DynamoDB partition and sort keys.
---

# Entities and Keys

_Every root entity type in the DynamoDB EF Core provider must declare a partition key, and optionally a sort key, which together form the item's primary key in DynamoDB._

## Defining an Entity

Root entity types (non-owned, non-derived) map to DynamoDB tables and must resolve table keys.
Key mapping can be explicit (`HasPartitionKey(...)`, `HasSortKey(...)`) or convention-based.
Root entities are independent DynamoDB items; the provider does not support EF Core foreign-key or
navigation relationships between them.

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasPartitionKey(x => x.CustomerId);
    b.HasSortKey(x => x.OrderId); // optional
});
```

!!! warning "Do not use HasKey or [Key]"

```
`HasKey(...)` and `[Key]` are rejected for root DynamoDB entities.
Configure keys with `HasPartitionKey(...)` and optional `HasSortKey(...)` instead.
```

If no explicit `ToTable(...)` is configured, the provider uses the CLR type name as the table
name.

!!! warning "No EF relationships"

```
`HasOne(...)`, `HasMany(...)`, `WithOne(...)`, `WithMany(...)`, `HasForeignKey(...)`,
`[ForeignKey]`, and `[InverseProperty]` are not supported. Use complex types for embedded
data, or model separate DynamoDB items/tables as separate root entities and join them in
application code when needed.
```

## Defaults and Overrides

For root entity types, table/key mapping resolves in this order (highest precedence first):

1. Explicit configuration (`ToTable(...)`, `HasPartitionKey(...)`, `HasSortKey(...)`)
2. Conventions (`PK`/`PartitionKey`, fallback `Id`, `SK`/`SortKey`; table name from CLR type)
3. Validation outcome (missing partition key throws; partition key resolved with no sort key
    means partition-key-only)

`HasKey(...)` and `[Key]` are not DynamoDB key overrides.

## Partition Key

Every DynamoDB table has a partition key. Configure it with `HasPartitionKey(...)` or use
conventional property names (`PK` or `PartitionKey`, case-insensitive). If neither DynamoDB-specific
name exists, `Id` is used as a fallback partition key name. `Id` does not create ambiguity when `PK`
or `PartitionKey` also exists; the DynamoDB-specific name wins.

`HasPartitionKey(...)` overrides convention-based partition key discovery.

Partition keys must be mapped, non-nullable EF properties and resolve to DynamoDB key-supported
provider types (string, number, or binary).

## Key Value Generation

DynamoDB does not generate primary key values for you. By convention, string and numeric partition
or sort keys are application-assigned: set them before calling `SaveChangesAsync`.

```csharp
context.Orders.Add(new Order
{
    CustomerId = "CUST#123",
    OrderId = 42,
    Description = "New order"
});
await context.SaveChangesAsync();
```

If a numeric key is left unset, EF Core treats the CLR default (for example `0`) as the value to
write. If a string key is left `null`, the save fails because DynamoDB key attributes must be
present and non-null.

Single-property `Guid` keys keep EF Core's default client-side generation behavior. Leaving such a
`Guid` key as `Guid.Empty` lets EF Core assign a new Guid before writing the item.

Composite DynamoDB keys are application-assigned by convention, even when one key part is a `Guid`.
Set both the partition key and sort key before saving, or explicitly configure a client-side value
generator for the key part that should be generated.

```csharp
public sealed class Session
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}

modelBuilder.Entity<Session>(b =>
{
    b.HasPartitionKey(x => x.Id);
});
```

Explicit EF Core value-generation configuration still wins over provider conventions when you need
a custom client-side generator. DynamoDB still does not generate the value; your EF Core
configuration must produce a concrete CLR value before save. For example, use a custom generator for
Guid v7 keys:

```csharp
public sealed class GuidV7ValueGenerator : ValueGenerator<Guid>
{
    public override bool GeneratesTemporaryValues => false;

    public override Guid Next(EntityEntry entry)
        => Guid.CreateVersion7();
}

modelBuilder.Entity<Session>(b =>
{
    b.HasPartitionKey(x => x.Id);
    b.Property(x => x.Id)
        .ValueGeneratedOnAdd()
        .HasValueGenerator<GuidV7ValueGenerator>();
});
```

A value generator creates the CLR key value. A value converter only changes how that value is stored
in DynamoDB.

## Sort Key

Sort keys are optional. When present, the table key shape is `[partitionKey, sortKey]`.

Configure with `HasSortKey(...)` or use conventional property names (`SK` or `SortKey`,
case-insensitive).

`HasSortKey(...)` overrides convention-based sort key discovery.

## Composite Keys

In DynamoDB, a composite table key means exactly two parts: partition key + sort key.

!!! warning "Composite keys must use partition/sort mapping"

```
`HasKey(...)` and `[Key]` are not the source of truth for composite DynamoDB keys.
Configure composite keys with `HasPartitionKey(...)` + `HasSortKey(...)`. If an EF primary key
shape diverges from the configured DynamoDB key mapping, model finalization throws.
```

When a sort key is configured (explicitly or by convention), the provider derives the EF primary
key as `[partitionKey, sortKey]` in that order.

Explicit composite-key mapping:

```csharp
modelBuilder.Entity<Order>(b =>
{
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

```
- Configuring `HasKey(...)` or `[Key]` on the entity
- Declaring a sort key without a resolvable partition key
- Ambiguous conventional names (both `PK` and `PartitionKey`, or both `SK` and `SortKey`)
```

See [Table and Key Mapping](../configuration/table-key-mapping.md) for full validation rules,
key-property requirements, and advanced mapping patterns.

## See also

- [Table and Key Mapping](../configuration/table-key-mapping.md)
- [Complex Properties and Collections](complex-types.md)
- [Secondary Indexes](secondary-indexes.md)
- [DynamoDB Concepts for EF Developers](../dynamodb-concepts.md)
