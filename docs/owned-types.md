---
icon: lucide/boxes
---

# Owned Types

The DynamoDB provider supports Entity Framework Core owned entity types (`OwnsOne`, `OwnsMany`) for
embedding complex object graphs within a single DynamoDB item.

## Overview
- Owned references are stored as nested maps (`AttributeValue.M`).
- Owned collections are stored as lists of maps (`AttributeValue.L`).
- Nested ownership is supported (owned types can contain other owned types).

## Configuration

### OwnsOne

```csharp
modelBuilder.Entity<Customer>(b =>
{
    b.OwnsOne(x => x.Profile);
});
```

### OwnsMany

```csharp
modelBuilder.Entity<Customer>(b =>
{
    b.OwnsMany(x => x.Orders);
});
```

### Nested ownership

```csharp
modelBuilder.Entity<Customer>(b =>
{
    b.OwnsOne(x => x.Profile, pb =>
    {
        pb.OwnsOne(x => x.Address, ab =>
        {
            ab.OwnsOne(x => x.Geo);
        });
    });
});
```

### Custom containing attribute names

Use `HasAttributeName(...)` to store the owned navigation under a custom attribute name.

```csharp
modelBuilder.Entity<Customer>(b =>
{
    b.OwnsOne(x => x.Profile, pb =>
    {
        pb.HasAttributeName("UserProfile");
    });
});
```

## Storage model

### Owned reference (`OwnsOne`)

```json
{
  "Pk": { "S": "CUSTOMER#1" },
  "Profile": {
    "M": {
      "DisplayName": { "S": "Ada" },
      "Age": { "N": "31" }
    }
  }
}
```

### Owned collection (`OwnsMany`)

```json
{
  "Pk": { "S": "CUSTOMER#1" },
  "Orders": {
    "L": [
      {
        "M": {
          "OrderNumber": { "S": "A-100" },
          "Total": { "N": "49.95" }
        }
      }
    ]
  }
}
```

Missing attributes and explicit DynamoDB `NULL` (`AttributeValue.NULL == true`) are handled with the
same semantics during materialization.

## Query behavior

DynamoDB PartiQL does not provide full nested-path translation support for provider query translation,
so the provider projects top-level attributes and extracts owned values client-side.

```csharp
var query = context.Customers
    .Select(x => new { x.Pk, City = x.Profile.Address.City });
```

Generated PartiQL shape:

```sql
SELECT Pk, Profile
FROM Customers
```

`Profile` is read from DynamoDB, then `Address.City` is extracted during client-side shaping.

## Null handling
- Optional owned navigation missing or `NULL`: materializes as `null`.
- Required owned navigation missing or `NULL`: throws during materialization.
- Optional owned navigation chains null-propagate in projections.

Example:

```csharp
var rows = await context.Customers
    .Select(x => new { x.Pk, Age = x.Profile.Age })
    .ToListAsync();
```

If `Profile` is null, `Age` is null rather than throwing.

## Limitations
- Nested owned member predicates/orderings are not translated (for example
  `Where(x => x.Profile.Address.City == "Seattle")`).
- Direct querying of owned collections via `SelectMany` is not translated.
- `.Include()` is not required for owned types and has no effect.

For current supported workarounds, see [Limitations](limitations.md).

## See also
- [Projections](projections.md)
- [Limitations](limitations.md)
- [EF Core Owned Entity Types](https://learn.microsoft.com/en-us/ef/core/modeling/owned-entities)
- [DynamoDB AttributeValue](https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_AttributeValue.html)
