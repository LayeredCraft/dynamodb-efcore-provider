---
title: Projection
description: How Select expressions are translated and which projections are evaluated server-side vs client-side.
---

# Projection

_`Select` expressions translate to an explicit PartiQL column list; projections that contain computed expressions — string transforms, arithmetic, constructor calls — are applied client-side after the server returns the raw attribute values._

## Basic Select

Three projection shapes are supported: entity, scalar, and DTO/anonymous.

```csharp
// Entity projection — all mapped properties are selected
var orders = await db.Orders.ToListAsync(cancellationToken);
// SELECT "OrderId", "CustomerId", "Status", "Total", "CreatedAt" FROM "Orders"

// Scalar projection — single column
var ids = await db.Orders.Select(o => o.OrderId).ToListAsync(cancellationToken);
// SELECT "OrderId" FROM "Orders"

// DTO / anonymous projection — only the named columns
var summaries = await db.Orders
    .Select(o => new { o.OrderId, o.Status, o.Total })
    .ToListAsync(cancellationToken);
// SELECT "OrderId", "Status", "Total" FROM "Orders"
```

The provider never emits `SELECT *`. Every projection results in an explicit column list, which keeps attribute reads predictable and consistent with EF Core type mapping.

## Server-Side Projections

Property reads for entity, scalar, and DTO leaf attributes execute server-side: the generated PartiQL lists each attribute by name, and DynamoDB returns only those attributes in the response. Duplicate attributes in a projection are deduplicated before the SQL is generated.

Shadow properties — such as the discriminator column for shared-table entities — are included in
the projection automatically when the materializer needs them, even if they have no corresponding
CLR member.

## Client-Side Projections

Computed expressions in a `Select` lambda run in-process after the server returns results. DynamoDB returns the raw attribute values; the shaper lambda applies the computation during materialization.

```csharp
var results = await db.Products
    .Select(p => new
    {
        p.ProductId,
        NameUpper = p.Name.ToUpper(),        // client-side: string transform
        DiscountedPrice = p.Price * 0.9m     // client-side: arithmetic
    })
    .ToListAsync(cancellationToken);

// Server returns "ProductId", "Name", "Price"
// Client computes ToUpper() and multiplication during shaping
```

This means the server always fetches the underlying attributes; there is no server-side function evaluation for computed projection expressions. Plan your attribute reads accordingly — if a projection reads several attributes to compute one value, all of those attributes are fetched from DynamoDB.

## Primitive Collection Properties

Entity properties typed as collections materialize from DynamoDB set and list attribute types:

| Property CLR Type                                           | DynamoDB Attribute Type | Materializes As                      |
| ----------------------------------------------------------- | ----------------------- | ------------------------------------ |
| `List<T>`, `IList<T>`                                       | `L`                     | `List<T>`                            |
| `T[]`                                                       | `L`                     | `T[]`                                |
| `HashSet<T>`, `ISet<T>`, `IReadOnlySet<T>`                  | `SS` / `NS` / `BS`      | `HashSet<T>`                         |
| `Dictionary<string, TValue>`, `IDictionary<string, TValue>` | `M`                     | `Dictionary<string, TValue>`         |
| `IReadOnlyDictionary<string, TValue>`                       | `M`                     | `ReadOnlyDictionary<string, TValue>` |

Interface-typed properties receive a concrete instance that implements the declared interface. Collection properties are selected by name like any scalar; their contents are deserialized from the `AttributeValue` during shaping.

## Complex Property Projections

Complex properties are stored as DynamoDB map attributes (`M`), and complex collections are stored
as DynamoDB lists (`L`) whose elements contain nested maps when needed. In both cases, the query
projects the top-level attribute name and the provider materializes the nested structure
client-side during shaping.

```csharp
// "Profile" is a complex property stored as a map attribute
var profiles = await db.Users
    .Select(u => new { u.UserId, u.Profile.DisplayName })
    .ToListAsync(cancellationToken);
// Server returns "UserId" and "Profile"; client extracts "DisplayName" from the map
```

!!! note

    Nested complex-property paths (`u.Profile.Address.City`) are supported in `Where` predicates,
    translating to dot-notation in PartiQL (`"Profile"."Address"."City"`). In `Select`
    projections, however, only the top-level complex attribute is projected by the server; deeper
    extraction happens client-side.

!!! warning

    Complex-property materialization is shape-strict. If DynamoDB returns a complex property under
    the expected attribute name but the wire shape is not a map (`M`), the provider throws during
    shaping instead of treating the value as `null`.

## See also

- [Supported Operators](operators.md)
- [How Queries Execute](how-queries-execute.md)
- [Complex Properties and Collections](../modeling/complex-types.md)
