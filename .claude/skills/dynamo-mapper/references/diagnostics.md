# Diagnostics

## Quick map

- `DM0001` unsupported property type -> switch to a supported type, ignore it, or custom-convert it
- `DM0003` unsupported collection element/shape -> flatten it, change element type, or
  custom-serialize it
- `DM0004` dictionary key must be string -> use `Dictionary<string, T>`
- `DM0005` incompatible `DynamoKind` -> remove the override or use a compatible kind
- `DM0006` nested cycle -> break the cycle, ignore a back-reference, or custom-convert one side
- `DM0007` unsupported nested member -> fix or ignore that nested member
- `DM0008` invalid dot path -> fix the path, or include base properties if inheritance is involved
- `DM0009` helper rendering limit -> likely generator issue
- `DM0101` no mapper methods found -> add a valid `To*` or `From*` method
- `DM0102` mismatched model types -> make both directions use the same model type
- `DM0103` multiple constructor attributes -> leave only one `[DynamoMapperConstructor]`

## Important non-diagnostic failure mode

Bad custom converter names or signatures may fail as ordinary C# compile errors instead of
DynamoMapper diagnostics.
