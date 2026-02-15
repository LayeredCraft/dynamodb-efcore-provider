---
icon: lucide/table
---

# Projections

## Supported shapes
- Entity projections (`Select(x => x)`).
- Scalar projections (`Select(x => x.Property)`).
- DTO and anonymous type projections.

## Behavior
- The provider emits explicit projection columns.
- Duplicate selected attributes are de-duplicated.
- Some computed projection expressions are evaluated client-side.

## Server-side vs client-side

### Server-side (PartiQL)
- Property selection for entity/scalar/DTO leaf attributes.
- `WHERE` and `ORDER BY` combined with projected columns.

### Client-side (after read)
- Some computed projection expressions (for example string transforms and arithmetic in projection lambdas).
- Null receiver behavior follows normal .NET runtime behavior.

## Example

```csharp
var rows = await db.SimpleItems
    .Select(x => new
    {
        x.Pk,
        Upper = x.StringValue.ToUpper(),
        DoubleValue = x.IntValue * 2
    })
    .ToListAsync();
```

- The query projects the required leaf attributes.
- `ToUpper()` and multiplication are applied in-memory during shaping.

## What works today
- Entity and scalar projections are stable and covered by integration tests.
- DTO constructor and object-initializer projections are supported.
- Projection deduplication avoids selecting the same attribute multiple times.

## AttributeValue model context
- DynamoDB request parameters and response values use `AttributeValue` wire types such as `S`, `N`,
  `BOOL`, `NULL`, `B`, `M`, `L`, and set types.
- Numeric values are transferred as strings on the wire (`N`, `NS`).

## Provider materialization coverage (today)
- Direct reads are implemented for `S`, `N`, `BOOL`, `NULL`, `B`, `M`, `L`, `SS`, `NS`, and `BS`.
- Additional CLR types (for example `Guid`, `DateTimeOffset`) are supported through EF Core value
  converters.

## Primitive collection materialization
- Supported property CLR shapes:
  - lists: `T[]`, `List<T>`, `IList<T>`, `IReadOnlyList<T>`
  - sets: `HashSet<T>`, `ISet<T>`, `IReadOnlySet<T>`
  - dictionaries with string keys: `Dictionary<string,TValue>`, `IDictionary<string,TValue>`,
    `IReadOnlyDictionary<string,TValue>`, `ReadOnlyDictionary<string,TValue>`
- Materialization uses provider concrete instances:
  - lists materialize as `List<T>` (or `T[]` for array properties)
  - sets materialize as `HashSet<T>`
  - dictionaries materialize as `Dictionary<string,TValue>` (or `ReadOnlyDictionary<string,TValue>`
    when property type is read-only dictionary)
- Interface-typed properties receive assignable concrete values (`Dictionary`/`HashSet`/`List`).

## Owned types

The provider supports EF Core owned entity types for embedding complex object graphs within a single
DynamoDB item.

- Owned navigations are discovered automatically by convention for complex CLR types.
- Explicit `OwnsOne`/`OwnsMany` configuration is still supported when you need to override defaults.

### Storage model
- **Owned references** (`OwnsOne`): stored as `AttributeValue.M` (nested map)
- **Owned collections** (`OwnsMany`): stored as `AttributeValue.L` (list of maps)
- Nested ownership is supported recursively (owned types can contain other owned types)

### Query behavior
Due to DynamoDB PartiQL limitations, the provider **always projects top-level attributes only** and
extracts nested owned values client-side during materialization.

Example:
```csharp
var query = context.Items
    .Select(x => new { x.Pk, x.Profile.Address.City });
```

Generated PartiQL:
```sql
SELECT Pk, Profile
FROM Items
```

The `Profile` attribute (an `AttributeValue.M`) is projected as a whole, then `Address.City` is
extracted during client-side materialization.

### Null handling
- Missing owned navigation attribute: materializes as `null` (for optional owned) or throws (for
  required owned)
- Explicit DynamoDB NULL (`AttributeValue.NULL == true`): same behavior as missing
- Optional owned navigation chains null-propagate: `x.Profile.Address.City` returns `null` if
  `Profile` is null

### Limitations
- Nested path queries not supported in `WHERE`/`ORDER BY` (e.g.,
  `.Where(x => x.Profile.Address.City == "Seattle")`)
- Owned types cannot be queried directly; must query via root entity
- See [Limitations](limitations.md) for details

## Notes
- Client-side computed projections follow normal .NET null behavior.

## External references
- AWS AttributeValue model: <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_AttributeValue.html>
