# Type Matrix

## Root scalars

- `string`, `bool`, `int`, `long`, `float`, `double`, `decimal`
- `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`
- enums
- nullable versions of supported scalar types

## Collections

Supported root collection shapes:

- arrays, `List<T>`, `IList<T>`, `ICollection<T>`, `IEnumerable<T>`
- `Dictionary<string, T>`, `IDictionary<string, T>`
- `HashSet<T>`, `ISet<T>`

Important note:

- `byte[]` is supported as a byte collection, not as a special binary scalar type

## Supported collection elements

- `string`, `bool`, `byte`, `short`, `int`, `long`, `float`, `double`, `decimal`
- `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`
- enums
- many nullable variants in list/map shapes
- `byte[]`
- nested objects in lists and maps

Set families:

- string -> `SS`
- numeric -> `NS`
- `byte[]` -> `BS`

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
