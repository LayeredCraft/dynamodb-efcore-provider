# Gotchas

## Do not teach these incorrectly

- Do not tell users to decorate every domain-model property.
- Do not require methods to be named exactly `ToItem` and `FromItem`.
- Do not teach lifecycle hooks as currently implemented behavior.
- Do not use the old property-level converter signatures from stale docs.
- Do not assume every converter mistake becomes a DynamoMapper diagnostic.

## Hard limits

- dictionary keys must be `string`
- nested collections like `List<List<string>>` are not supported
- nested object cycles are rejected
- nested objects are not supported inside sets
- constructor parameter matching uses .NET property names, not attribute names
- empty sets are omitted because DynamoDB does not allow them

## Stale-doc corrections

- nested mapping is supported
- hook docs are stale for current behavior
- static converter docs are stale on signatures and constraints
- some prose docs mention diagnostics that do not exist

## If unsure

- prefer simple mapper classes
- prefer supported scalar and collection shapes
- avoid promising hook behavior
- avoid inventing converter signatures from memory
