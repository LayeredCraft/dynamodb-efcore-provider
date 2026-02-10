---
icon: lucide/triangle-alert
---

# Limitations

## Not supported yet
- `SaveChanges` and `SaveChangesAsync`.
- Synchronous query enumeration.
- `ToQueryString()` support for the custom querying enumerable.
- Large parts of LINQ translation surface (see `operators.md`).
- Method calls in `Where` predicates (for example `StartsWith`, `ToUpper`).
- Collection predicate translation (for example `List.Contains`, `Set.Contains`, `Dictionary.ContainsKey`).
- Provider option for `ConsistentRead`.

## Primitive collection constraints
- Primitive collection property mapping/materialization is supported for lists/arrays (`L`), maps (`M`), and sets (`SS`/`NS`/`BS`).
- Dictionary/map keys must be `string`.
- Set element provider types must map to DynamoDB set types (`string`, numeric, or `byte[]`).

## What this means in practice
- The provider is currently query-only.
- Unsupported LINQ shapes typically fail during translation with `InvalidOperationException` or `NotImplementedException`.
- `WithoutPagination()` is best-effort mode and can return incomplete results.

## Operator-specific status
- Use `operators.md` as the canonical source for supported and unsupported operators.
