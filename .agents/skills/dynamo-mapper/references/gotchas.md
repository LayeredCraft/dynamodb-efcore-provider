# Gotchas

## Do not teach these incorrectly

- Do not tell users to decorate every domain-model property.
- Do not require methods to be named exactly `ToItem` and `FromItem`.
- Do not invent lifecycle hook signatures from memory.
- Do not use the old property-level converter signatures from stale docs.
- Do not assume every converter mistake becomes a DynamoMapper diagnostic.

## Hard limits

- dictionary keys must be `string`
- nested collections like `List<List<string>>` are not supported
- nested object cycles are rejected
- nested objects are not supported inside sets
- constructor parameter matching uses .NET property names, not attribute names
- empty sets are omitted because DynamoDB does not allow them
- `OmitNullStrings` is legacy and misnamed; prefer `OmitNullValues` for mapper-level null omission,
  especially for nested object and nested collection containers

## Stale-doc corrections

- nested mapping is supported
- lifecycle hooks are implemented with strict signature validation
- static converter docs are stale on signatures and constraints
- some prose docs mention diagnostics that do not exist

## If unsure

- prefer simple mapper classes
- prefer supported scalar and collection shapes
- use the exact four hook names and signatures when hooks are needed
- avoid inventing converter signatures from memory
