---
icon: lucide/table
---

# Projections

## Supported shapes
- Entity projections (`Select(x => x)`).
- Scalar projections (`Select(x => x.Property)`).
- DTO and anonymous type projections.
- Primitive collection properties on entities:
  - lists/arrays (`L`),
  - string-keyed dictionaries/maps (`M`),
  - sets (`SS`, `NS`, `BS`).

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
- Direct reads are implemented for `S`, `N`, `BOOL`, `NULL`, and `B`.
- Collection reads are implemented for `L`, `M`, `SS`, `NS`, and `BS`.
- Primitive CLR mappings are direct for `string`, `bool`, `byte[]`, and numeric types
  `byte`, `short`, `int`, `long`, `float`, `double`, `decimal`.
- Primitive collection CLR mappings include:
  - `List<T>`, `IList<T>`, `IReadOnlyList<T>`, `IEnumerable<T>`, and arrays (`T[]`),
  - `Dictionary<string, TValue>`, `IDictionary<string, TValue>`, `IReadOnlyDictionary<string, TValue>`,
  - `ReadOnlyDictionary<string, TValue>`,
  - `HashSet<T>`, `ISet<T>`, and `IReadOnlySet<T>`.
- Custom concrete collection types are supported when they implement the matching collection interface (`ICollection<T>` for lists/sets, `IDictionary<string, TValue>` for maps).
- `ReadOnlyMemory<byte>` is supported and stored as DynamoDB binary (`B`) via a value converter.
- Additional CLR types (for example `Guid`, `DateTimeOffset`) are supported through EF Core value
  converters.
- `EF.Property(...)` scalar projections are supported, including converter-backed types such as
  `Guid` and `DateTimeOffset`.

## Collection semantics and constraints
- Dictionary/map keys must be `string`.
- Set element provider types must map to DynamoDB set wire types:
  - `string` -> `SS`,
  - numeric types -> `NS`,
  - `byte[]` -> `BS`.
- Set equality in change tracking is order-insensitive.
- `ReadOnlyDictionary<string, TValue>` uses snapshot optimization (no deep-copy snapshot).

## Notes
- Client-side computed projections follow normal .NET null behavior.

## External references
- AWS AttributeValue model: <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_AttributeValue.html>
