# Diagnostics

## Quick map

- `DM0001` unsupported property type -> switch to a supported type, ignore it, or custom-convert it
- `DM0003` unsupported collection element/shape -> flatten it, change element type, or
  custom-serialize it
- `DM0004` dictionary key must be string -> use `Dictionary<string, T>`
- `DM0005` incompatible `DynamoKind` -> remove the override or use a compatible kind
- `DM0006` nested cycle -> break the cycle, ignore a back-reference, or custom-convert one side
- `DM0007` unsupported nested member -> fix or ignore that nested member
- `DM0008` invalid dot path -> fix the path; paths can traverse nested objects and collection
  element types (`"Items.ProductId"` targets `ProductId` on the element of `Items`); include base
  properties if inheritance is involved
- `DM0009` helper rendering limit -> likely generator issue
- `DM0101` no mapper methods found -> add a valid `To*` or `From*` method
- `DM0102` mismatched model types -> make both directions use the same model type
- `DM0103` multiple constructor attributes -> leave only one `[DynamoMapperConstructor]`
- `DM0401` invalid hook signature -> use exact hook name/signature and `void` return type
- `DM0402` hook not static -> declare hook as `static`
- `DM0403` hook parameter type mismatch -> use the mapper model type `T` and expected dictionary/ref

## Hook diagnostics (all warnings)

- `DM0401` covers wrong parameter count, wrong `ref` usage, non-void return type, or non-partial
  hook
- `DM0402` is emitted when a hook method is not static
- `DM0403` is emitted when hook parameter types do not match mapper model type/dictionary shape

## Important non-diagnostic failure mode

Bad custom converter names or signatures may fail as ordinary C# compile errors instead of
DynamoMapper diagnostics.
