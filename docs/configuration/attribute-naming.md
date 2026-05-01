---
title: Attribute Naming
description: How property names map to DynamoDB attribute names and how to customize them.
---

# Attribute Naming

_By default the provider applies CamelCase to property names in the model when resolving DynamoDB
attribute names.
You can switch to a different built-in convention, supply a custom naming function, or pin
individual properties to an explicit attribute name._

## Default Naming Convention

The provider applies **CamelCase** to every declared property that has no explicit attribute name
configured. This happens automatically at model finalization — you do not need to call anything to
get it.

| CLR property name | DynamoDB attribute name (default) |
| ----------------- | --------------------------------- |
| `CustomerId`      | `customerId`                      |
| `OrderDate`       | `orderDate`                       |
| `ShippingAddress` | `shippingAddress`                 |

This means the attribute names stored in DynamoDB will differ from the CLR property names unless
you override the convention.

!!! note "Key attributes follow the same convention"

    The naming convention applies to partition key and sort key properties as well. Your DynamoDB
    table's key schema must use the convention-transformed name. For example, a `CustomerId`
    property with the default CamelCase convention maps to DynamoDB attribute `"customerId"` — the
    table must define `"customerId"` as its hash key, not `"CustomerId"`.

## Built-in Conventions

Switch to a different convention for an entity type using `HasAttributeNamingConvention`:

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasPartitionKey(x => x.CustomerId);
    b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.SnakeCase);
});
```

Available values:

| Convention                | CLR name → DynamoDB attribute                  |
| ------------------------- | ---------------------------------------------- |
| `CamelCase` **(default)** | `OrderDate` → `orderDate`                      |
| `None`                    | `OrderDate` → `OrderDate` (CLR name unchanged) |
| `SnakeCase`               | `OrderDate` → `order_date`                     |
| `KebabCase`               | `OrderDate` → `order-date`                     |
| `UpperSnakeCase`          | `OrderDate` → `ORDER_DATE`                     |

Built-in conventions use Humanizer-style word splitting, so acronyms map naturally (for example,
`URLValue` → `url_value`, `PK` → `pk`).

Use `None` to opt out of all automatic transformation and keep CLR names as-is:

```csharp
b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.None);
```

## Custom Naming Function

Pass a `Func<string, string>` delegate for transformations not covered by the built-in options:

```csharp
b.HasAttributeNamingConvention(name => name.ToUpperInvariant());
```

The delegate receives the model property name (including user-defined shadow properties) and
returns the DynamoDB attribute name. It is called once per declared property during model
finalization.

!!! note "Scope of convention-based renaming"

    Convention-based renaming applies to EF model members (entity properties, key properties, and
    complex-property container/member attributes). It does not rewrite ad-hoc nested map keys provided
    as runtime data values (for example, dictionary entry keys).

## Per-Property Override

`HasAttributeName` on a property always wins over any convention — entity-level or default:

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasPartitionKey(x => x.CustomerId);
    b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.SnakeCase);

    // Stored as "PK" even though SnakeCase would produce "customer_id"
    b.Property(x => x.CustomerId).HasAttributeName("PK");
    b.Property(x => x.OrderId).HasAttributeName("SK");
});
```

This is the recommended pattern when your DynamoDB table uses short, generic key names (`PK`,
`SK`) but your CLR model uses descriptive property names.

## Complex Properties

Complex properties inherit the naming convention from their root entity. If the root uses
SnakeCase, nested complex members also use SnakeCase unless you override the complex property
itself:

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("Orders");
    b.HasPartitionKey(x => x.CustomerId);
    b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.SnakeCase);

    b.ComplexProperty(x => x.ShippingAddress, ab =>
    {
        ab.HasAttributeName("shipping_address");
    });
});
```

The complex property's own container attribute name is also convention-transformed. In the example
above, the root convention would normally produce `"shippingAddress"`, but the explicit
`HasAttributeName("shipping_address")` override wins.

Nested scalar members still inherit the root naming convention:

```csharp
// ShippingAddress.City -> "city"
// ShippingAddress.PostalCode -> "postal_code" when SnakeCase is active
```

## Precedence

When multiple sources could apply a name to the same property, the provider resolves them in this
order (highest to lowest):

1. Explicit `HasAttributeName(...)` on the property
1. Entity-level `HasAttributeNamingConvention(...)` set directly on the entity type
1. Convention inherited by nested complex properties from the root entity
1. Provider default: **CamelCase**

## See also

- [Table and Key Mapping](table-key-mapping.md)
- [Data Modeling](../modeling/index.md)
