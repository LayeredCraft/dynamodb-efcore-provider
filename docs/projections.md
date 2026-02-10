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
- Direct reads are implemented for `S`, `N`, `BOOL`, `NULL`, and `B`.
- Additional CLR types (for example `Guid`, `DateTimeOffset`) are supported through EF Core value
  converters.

## Notes
- Client-side computed projections follow normal .NET null behavior.

## External references
- AWS AttributeValue model: <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_AttributeValue.html>
