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
- Discriminator guardrails for unsupported query shapes are deferred; support is limited to the
  current operator surface in `operators.md`.

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

## Key mapping validation limits
- Shared-table mappings must agree on key shape: all entity types mapped to the same table must be either PK-only or PK+SK.
- Shared-table mappings must use consistent physical PK/SK attribute names across entity types.
- Key properties must resolve to DynamoDB key-compatible provider types: string, number, or binary (`byte[]`).
- `bool` key mappings are rejected.
- Converter-backed key mappings are validated against the converter provider CLR type.
- Key properties must be required/non-nullable, and converter provider types for keys must also be non-nullable.

## Shared-table discriminator limits
- Shared-table mappings with multiple concrete entity types require a discriminator.
- The default discriminator attribute name is `$type` (configurable via EF Core
  `HasEmbeddedDiscriminatorName`).
- Default discriminator values are EF Core type short names (for example `UserEntity`).
- Discriminator values must be unique within a shared table group.
- Discriminator attribute names must be consistent across all entity types in a shared table group.
- Discriminator attribute names must not collide with resolved PK/SK attribute names.
- Missing or unknown discriminator values in returned items throw during materialization.

## Owned types query limitations

### Nested path queries (not supported)
DynamoDB PartiQL does not support querying nested attributes directly. The following queries are
not supported:

```csharp
// ❌ Not supported: filtering on owned property
context.Items.Where(x => x.Profile.Address.City == "Seattle")

// ❌ Not supported: ordering by owned property
context.Items.OrderBy(x => x.Profile.Age)
```

**Workaround:** Use `AsAsyncEnumerable()` to switch to client-side evaluation:
```csharp
// ✅ Supported: client-side filter after materialization
var filtered = await context.Items
    .AsAsyncEnumerable()
    .Where(x => x.Profile?.Address?.City == "Seattle")
    .ToListAsync();
```

### Direct owned collection queries (not supported)
You cannot query an owned collection directly:

```csharp
// ❌ Not supported
context.Items.SelectMany(x => x.Orders).Where(o => o.Total > 100)
```

**Workaround:** Use `AsAsyncEnumerable()` to switch to LINQ-to-objects:
```csharp
// ✅ Supported
var orders = await context.Items
    .AsAsyncEnumerable()
    .SelectMany(x => x.Orders)
    .Where(o => o.Total > 100)
    .ToListAsync();
```

### Owned types and Include (not applicable)
Owned types are always included in the root entity query; explicit `.Include()` is not needed
(and will be ignored).

For complete owned type behavior and examples, see [Owned Types](owned-types.md).
