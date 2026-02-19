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
- By convention, navigation target types that are not DynamoDB primitive-mapped CLR types and not
  supported primitive collection shapes are registered as owned during model discovery.
- Explicit `OwnsOne`/`OwnsMany` configuration is still supported when you need to override defaults.
- For owned collections discovered this way, the provider adds a synthetic ordinal key at model
  finalization for stable identity and change tracking.
- Owned references materialize from `AttributeValue.M`; owned collections materialize from
  `AttributeValue.L`.
- Query translation still projects top-level attributes only, then extracts nested owned members
  client-side during shaping.
- Dictionary-valued owned navigations (for example `Dictionary<string, OwnedType>`) are not
  translated/materialized yet; use `OwnsMany` collections for now.

For full configuration, null behavior, storage examples, and limitations, see
[Owned Types](owned-types.md).

## Notes
- Client-side computed projections follow normal .NET null behavior.

## External references
- AWS AttributeValue model: <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_AttributeValue.html>
