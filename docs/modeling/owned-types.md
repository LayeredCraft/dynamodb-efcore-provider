---
title: Owned Types and Collections
description: How owned entities and owned collections are stored and queried in DynamoDB.
---

# Owned Types and Collections

_Owned types are stored inline within the owning entity's DynamoDB item as nested maps or lists, with no separate table or key._

## Owned Entities

`OwnsOne<T>()` maps a navigation to a DynamoDB Map (`AttributeValue.M`) embedded within the
owning item. Any non-primitive, non-collection navigation that EF Core can discover by convention
is automatically treated as owned — you don't always need to call `OwnsOne` explicitly.

```csharp
modelBuilder.Entity<Customer>(builder =>
{
    builder.ToTable("Customers");
    builder.HasPartitionKey(x => x.Pk);
    builder.OwnsOne(x => x.Profile);
});
```

Under the default CamelCase naming convention, the `Profile` property is stored as `"profile"` in
DynamoDB:

```json
{
  "pk": { "S": "CUSTOMER#1" },
  "profile": {
    "M": {
      "displayName": { "S": "Ada" },
      "age": { "N": "31" }
    }
  }
}
```

To store the owned type under a different attribute name, use `HasAttributeName(...)` on the
navigation:

```csharp
builder.OwnsOne(x => x.Profile, profile =>
{
    profile.HasAttributeName("userProfile");
});
```

!!! note "Containing attribute name follows the naming convention"

    The DynamoDB attribute key for the owned Map — `"profile"` for a `Profile` property — is
    subject to the root entity's naming convention, just like any other property. Use
    `HasAttributeName(...)` on the navigation builder to override it. See
    [Attribute Naming](../configuration/attribute-naming.md) for how conventions propagate to
    owned types.

## Owned Collections

`OwnsMany<T>()` maps a collection navigation to a DynamoDB List of Maps (`AttributeValue.L`).

```csharp
builder.OwnsMany(x => x.Contacts, contact =>
    contact.OwnsOne(x => x.Address));
```

The collection is stored as a List:

```json
{
  "pk": { "S": "CUSTOMER#1" },
  "contacts": {
    "L": [
      { "M": { "email": { "S": "ada@example.com" } } }
    ]
  }
}
```

Supported CLR collection shapes: `T[]`, `List<T>`, `IList<T>`, `IReadOnlyList<T>`. Using
`ICollection<T>` throws at model finalization.

!!! note "Shadow ordinal property"

    The provider adds a shadow property `__OwnedOrdinal` (`int`) to each owned collection element
    type for change tracking identity. It does not appear in DynamoDB and is not affected by
    attribute naming conventions.

## Query Behavior

### Filtering

Nested owned property paths translate to dot-notation in PartiQL, and list index access translates
to bracket-notation:

```csharp
context.Customers.Where(x => x.Profile.Address.City == "Seattle")
```

```sql
WHERE "profile"."address"."city" = 'Seattle'
```

```csharp
context.Customers.Where(x => x.Tags[0] == "featured")
```

```sql
WHERE "tags"[0] = 'featured'
```

### Projections

Nested path access in `Select` is not supported server-side. The provider projects the top-level
owned container and extracts nested values on the client:

```csharp
context.Customers.Select(x => new { x.Pk, City = x.Profile.Address.City })
```

The generated PartiQL selects the whole container:

```sql
SELECT "pk", "profile" FROM "Customers"
```

`profile` is read from DynamoDB and `Address.City` is extracted during client-side shaping.

!!! warning "SelectMany is not translated"

    Direct querying of owned collections via `SelectMany` is not supported. `.Include()` has no
    effect on owned types and can be omitted.

## Nesting Limits and Constraints

!!! warning "DynamoDB item size limit"

    DynamoDB imposes a maximum item size of 400 KB. Deeply nested or large owned collections
    count against this limit.

Owned types can be nested to any depth — an owned collection can contain owned references, which
can themselves contain further owned types:

```csharp
builder.OwnsOne(x => x.Profile, profile =>
{
    profile.OwnsOne(x => x.PreferredAddress);
    profile.OwnsOne(x => x.BillingAddress);
});

builder.OwnsMany(x => x.Contacts, contact =>
    contact.OwnsOne(x => x.Address));
```

Null and missing attribute handling:

| Scenario                                                 | Behavior                           |
| -------------------------------------------------------- | ---------------------------------- |
| Optional navigation missing or `NULL` in DynamoDB        | Materializes as `null`             |
| Required navigation missing or `NULL` in DynamoDB        | Throws `InvalidOperationException` |
| Optional navigation chain null-propagates in projections | `null`, not an error               |

!!! warning "Strict materialization"

    Unlike the Cosmos DB EF provider, this provider does not silently skip missing required
    properties on owned types. If the DynamoDB attribute is absent or is a DynamoDB `NULL` and
    the EF property is required, materialization throws. Design your schemas accordingly.

## See also

- [Entities and Keys](entities-keys.md)
- [Attribute Naming](../configuration/attribute-naming.md)
- [Limitations](../limitations.md)
- [EF Core Owned Entity Types](https://learn.microsoft.com/en-us/ef/core/modeling/owned-entities)
- [DynamoDB AttributeValue](https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_AttributeValue.html)
