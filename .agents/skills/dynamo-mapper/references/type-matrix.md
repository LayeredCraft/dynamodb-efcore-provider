# Type Matrix

## Root scalars

- `string`, `bool`, `int`, `long`, `float`, `double`, `decimal`
- `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`
- `DateOnly`, `TimeOnly` (net6.0+ only; stored as DynamoDB `S` with configurable format)
  - `DateOnly` default format: `"yyyy-MM-dd"` — override via
    `[DynamoMapper(DateOnlyFormat = "...")]` or `[DynamoField(..., Format = "...")]`
  - `TimeOnly` default format: `"HH:mm:ss.fffffff"` — override via
    `[DynamoMapper(TimeOnlyFormat = "...")]` or `[DynamoField(..., Format = "...")]`
- enums
- nullable versions of supported scalar types

## Collections

Supported root collection shapes:

- arrays, `List<T>`, `IList<T>`, `ICollection<T>`, `IEnumerable<T>`
- `Dictionary<string, T>`, `IDictionary<string, T>`
- `HashSet<T>`, `ISet<T>`

Important note:

- `byte[]` is supported as a byte collection, not as a special binary scalar type
- set-like CLR shapes preserve their declared shape on round-trip, but DynamoDB storage depends on
  the element type

## Supported collection elements

- `string`, `bool`, `byte`, `short`, `int`, `long`, `float`, `double`, `decimal`
- `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`
- `DateOnly`, `TimeOnly` (net6.0+ only)
- enums
- many nullable variants in list/map shapes
- `byte[]`
- nested objects in lists and maps

Set families:

- string -> `SS`
- numeric -> `NS`
- `byte[]` -> `BS`

When a set-like CLR collection uses another supported element type such as `Guid`,
`DateTimeOffset`, `TimeSpan`, or enums, DynamoMapper stores it as `L` and materializes it back
into the declared CLR set shape on read.

## Nested shapes

Supported:

- nested object property
- list or array of nested objects
- `Dictionary<string, NestedType>`

## Unsupported or sharp edges

- root `byte` is not supported
- root `short` is not supported
- dictionary keys must be `string`
- nested collections like `List<List<string>>` are not supported
- nested objects are not supported inside sets
- direct or indirect nested cycles are rejected
- invalid dot paths become `DM0008`
- cycles become `DM0006`
