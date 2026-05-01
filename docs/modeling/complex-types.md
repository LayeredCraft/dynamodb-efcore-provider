---
title: Complex Properties and Collections
description: How complex properties and complex collections are stored and queried in DynamoDB.
---

# Complex Properties and Collections

_Complex properties are stored inline within the owning entity's DynamoDB item as nested maps or
lists, with no separate table or key._

## Complex Properties

`ComplexProperty(...)` maps a value-object-style member to a DynamoDB map (`AttributeValue.M`)
embedded within the owning item. Plain nested POCO members are auto-discovered by convention; use
`ComplexProperty(...)` when you want to customize the discovered member or configure it explicitly.

The CLR type may be declared as an EF Core complex type explicitly, typically with
`[ComplexType]` or `modelBuilder.ComplexType<T>()`, but that is not required for basic provider
discovery.

```csharp
[ComplexType]
public class CustomerProfile
{
    public string? DisplayName { get; set; }
    public int? Age { get; set; }
}

modelBuilder.Entity<Customer>(builder =>
{
    builder.ToTable("Customers");
    builder.HasPartitionKey(x => x.Pk);
    builder.ComplexProperty(x => x.Profile);
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

To store the complex property under a different attribute name, use `HasAttributeName(...)` on
the complex-property builder:

```csharp
builder.ComplexProperty(x => x.Profile, profile =>
{
    profile.HasAttributeName("userProfile");
});
```

!!! note "Container attribute names follow the naming convention"

    The DynamoDB attribute key for the complex-property map is subject to the root entity's naming
    convention, just like any other property. Use `HasAttributeName(...)` on the complex-property
    builder to override it. See [Attribute Naming](../configuration/attribute-naming.md) for how
    conventions propagate to nested complex properties.

!!! note "Explicit configuration is optional for basic discovery"

    Use `ComplexProperty(...)` when you need nested configuration such as explicit attribute names,
    converters, or other metadata overrides. For a plain nested POCO member, the provider
    discovers it automatically as a complex property.

## Complex Collections

`ComplexCollection(...)` maps a collection property to a DynamoDB list (`AttributeValue.L`).
Plain collection properties whose element type is a nested POCO are auto-discovered by
convention for the provider's supported complex collection CLR shapes: `List<T>` and `IList<T>`.
Use `ComplexCollection(...)` when you need to customize the discovered collection or its element
members.

Collection elements can themselves contain nested complex properties.

```csharp
builder.ComplexCollection(x => x.Contacts, contact =>
    contact.ComplexProperty(x => x.Address));
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

Supported CLR collection shapes: `List<T>`, `IList<T>`. `ICollection<T>`,
`IReadOnlyList<T>`, and arrays are not supported for complex collections.

!!! note "Collection updates replace the full list"

    Complex collection updates are written as full-list replacements of the containing DynamoDB
    attribute. Modifying, adding, or removing an element updates the entire list value, not an
    in-place list element delta.

## Query Behavior

### Filtering

Nested complex-property paths translate to dot-notation in PartiQL, and list index access
translates to bracket-notation:

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
complex-property container and extracts nested values on the client:

```csharp
context.Customers.Select(x => new { x.Pk, City = x.Profile.Address.City })
```

The generated PartiQL selects the whole container:

```sql
SELECT "pk", "profile" FROM "Customers"
```

`profile` is read from DynamoDB and `Address.City` is extracted during client-side shaping.

!!! warning "SelectMany is not translated"

    Direct querying of complex collections via `SelectMany` is not supported. `.Include()` has no
    effect on complex properties and can be omitted.

## Nesting Limits and Constraints

### Nesting and Size Limits

Complex properties are embedded in the same DynamoDB item as the root entity. That means all
nested data shares one item-size budget and is read or written as part of that root item.

!!! warning "DynamoDB item size limit"

    DynamoDB imposes a maximum item size of 400 KB. Deeply nested or large complex collections
    count against this limit.

Complex types can be nested to any depth. A complex collection can contain nested complex
properties, which can themselves contain further complex properties.

In practice, keep nesting depth and collection size intentional: deeper or larger graphs are
valid, but they increase item size and payload cost for every read and write of the owning entity.

```csharp
builder.ComplexProperty(x => x.Profile, profile =>
{
    profile.ComplexProperty(x => x.PreferredAddress);
    profile.ComplexProperty(x => x.BillingAddress);
});

builder.ComplexCollection(x => x.Contacts, contact =>
    contact.ComplexProperty(x => x.Address));
```

### Null and Missing Attribute Behavior

When reading complex properties, the provider distinguishes optional vs required semantics from
the EF model and applies them consistently for both missing attributes and explicit DynamoDB
`NULL` values.

Null and missing attribute handling:

| Scenario                                               | Behavior                           |
| ------------------------------------------------------ | ---------------------------------- |
| Optional complex property missing or `NULL` in DynamoDB | Materializes as `null`             |
| Required complex property missing or `NULL` in DynamoDB | Throws `InvalidOperationException` |
| Any complex property present with a non-map wire shape  | Throws `InvalidOperationException` |
| Optional complex path null-propagates in projections    | `null`, not an error               |

!!! warning "Strict materialization"

    Unlike the Cosmos DB EF provider, this provider does not silently skip missing required
    properties on complex types. If the DynamoDB attribute is absent or is a DynamoDB `NULL` and
    the EF property is required, materialization throws. Likewise, if a complex property is present
    but encoded with the wrong DynamoDB wire shape (for example `S` instead of `M`), materialization
    throws even when the CLR property is nullable. Design your schemas accordingly.

## See also

- [Entities and Keys](entities-keys.md)
- [Attribute Naming](../configuration/attribute-naming.md)
- [Limitations](../limitations.md)
- [EF Core Complex Types](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-8.0/whatsnew#value-objects-using-complex-types)
- [DynamoDB AttributeValue](https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_AttributeValue.html)
