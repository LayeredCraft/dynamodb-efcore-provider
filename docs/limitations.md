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
- Provider option for `ConsistentRead`.

## What this means in practice
- The provider is currently query-only.
- Unsupported LINQ shapes typically fail during translation with `InvalidOperationException` or `NotImplementedException`.
- `WithoutPagination()` is best-effort mode and can return incomplete results.

## Operator-specific status
- Use `operators.md` as the canonical source for supported and unsupported operators.

## Primitive collection CLR shape limits
- Primitive collections are supported only for specific CLR shapes.
- Custom or derived concrete collection types are rejected during model validation.
- Supported list shapes: `T[]`, `List<T>`, `IList<T>`, `IReadOnlyList<T>`.
- Supported set shapes: `HashSet<T>`, `ISet<T>`, `IReadOnlySet<T>`.
- Supported dictionary shapes (string keys only): `Dictionary<string,TValue>`,
  `IDictionary<string,TValue>`, `IReadOnlyDictionary<string,TValue>`, and
  `ReadOnlyDictionary<string,TValue>`.
