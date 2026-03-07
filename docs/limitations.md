---
icon: lucide/triangle-alert
---

# Limitations

## Not supported yet
- `SaveChanges` and `SaveChangesAsync`.
- Synchronous query enumeration.
- `ToQueryString()` support for the custom querying enumerable.
- Large parts of LINQ translation surface (see `operators.md`).
- Method calls in `Where` predicates except supported `Contains` patterns (`string.Contains(string)` and in-memory collection membership); other `string.Contains` overloads (such as `char` or `StringComparison`) are not translated.
- Index-aware query planning and PartiQL secondary-index targeting.
- Provider-side key encoding helpers (prefix/suffix composition).
- Provider option for `ConsistentRead`.

## What this means in practice
- The provider is currently query-only.
- Unsupported LINQ shapes fail during translation with `InvalidOperationException` including provider-specific details.
- `WithoutPagination()` is best-effort mode and can return incomplete results.
- Discriminator guardrails for unsupported query shapes are deferred; support is limited to the
  current operator surface in `operators.md`.

## Operator-specific status
- Use `operators.md` as the canonical source for supported and unsupported operators.

## Single-table mapping constraints
- A "table group" is the set of entity types mapped to the same DynamoDB table name.
- Only the table primary key (partition key and optional sort key) is modeled today.
- Secondary-index metadata can be configured, but query execution does not yet target GSIs/LSIs in generated PartiQL.
- Key encoding is not implemented as a provider feature; construct PK/SK values in your domain model.
- For table groups containing multiple concrete entity types, a discriminator is required and is
  validated at startup.
- For inheritance hierarchies, querying a base type can materialize derived CLR types; base-type
  hierarchy queries project hierarchy attributes needed for derived-type materialization.

## Primitive collection CLR shape limits
- Primitive collections are supported only for specific CLR shapes.
- Custom or derived concrete collection types are rejected during model validation.
- Supported list shapes: `T[]`, `List<T>`, `IList<T>`, `IReadOnlyList<T>`.
- Supported set shapes: `HashSet<T>`, `ISet<T>`, `IReadOnlySet<T>`.
- Supported dictionary shapes (string keys only): `Dictionary<string,TValue>`,
  `IDictionary<string,TValue>`, `IReadOnlyDictionary<string,TValue>`, and
  `ReadOnlyDictionary<string,TValue>`.

## Key mapping validation limits
- `HasKey(...)` alone does not configure DynamoDB table keys.
- Shared-table mappings must agree on key shape: all entity types mapped to the same table must be either PK-only or PK+SK.
- Shared-table mappings must use consistent physical PK/SK attribute names across entity types.
- Key properties must resolve to DynamoDB key-compatible provider types: string, number, or binary (`byte[]`).
- `bool` key mappings are rejected.
- Converter-backed key mappings are validated against the converter provider CLR type.
- Key properties must be required/non-nullable, and converter provider types for keys must also be non-nullable.

Practical implication:

```csharp
// Not enough for DynamoDB key mapping
modelBuilder.Entity<Order>()
    .HasKey(x => new { x.CustomerId, x.OrderId });

// Required DynamoDB-specific mapping
modelBuilder.Entity<Order>(b =>
{
    b.HasPartitionKey(x => x.CustomerId);
    b.HasSortKey(x => x.OrderId);
});
```

## Shared-table discriminator limits
- Shared-table mappings with multiple concrete entity types require a discriminator.
- The default discriminator attribute name is `$type` (configurable via EF Core
  `HasEmbeddedDiscriminatorName`).
- Default discriminator values are EF Core type short names (for example `UserEntity`).
- Discriminator values must be unique within a shared table group.
- Discriminator attribute names must be consistent across all entity types in a shared table group.
- Discriminator attribute names must not collide with resolved PK/SK attribute names.
- Missing or unknown discriminator values in returned items throw during materialization.
- Inheritance queries follow EF Core discriminator semantics:
  - `DbSet<BaseType>` materializes concrete types in that hierarchy.
  - `DbSet<DerivedType>` materializes the derived subtree.
  - Abstract types are never materialized.
- Base-type hierarchy queries project hierarchy attributes needed for derived-type materialization.

## Null comparison limitations

### Parameterized null inconsistency {#parameterized-null-inconsistency}
When a nullable variable is null at runtime (`x.Prop == someVar` where `someVar` is null),
the query is parameterized as `WHERE "Prop" = ?` with `AttributeValue { NULL = true }`.
This only matches attributes stored with the DynamoDB NULL type — it does **not** match
MISSING attributes. DynamoDB PartiQL does not support `attr IS ?` (parameterized IS), so
the consistent behavior of `== null` (constant) cannot be replicated for parameterized paths.

This is a DynamoDB engine limitation. If you need to match both NULL and MISSING via a
runtime variable, use `EF.Functions.IsNull(x.Prop) || EF.Functions.IsMissing(x.Prop)`.

### Two-column nullable comparison {#null-column-comparison}
Comparing two nullable columns directly (`x.A == x.B` where both are nullable) generates a
binary `=` predicate. This will not return correct results when either column holds a NULL
type or is MISSING, because DynamoDB PartiQL SQL semantics return MISSING (not TRUE) for
equality comparisons involving NULL. There is no workaround for this shape at the provider
level today.

## Owned types query limitations

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

### Nested path access in Select (not supported)

Nested owned property paths (`x.Profile.Address.City`) and list index access (`x.Tags[0]`) are
translated to PartiQL in `Where` predicates only. Using them in a `Select` projection falls back
to client-side extraction from the top-level owned container — the full owned container is
fetched from DynamoDB rather than projecting just the nested attribute.

```csharp
// ✅ Supported: translates to WHERE "Profile"."Address"."City" = 'Seattle'
context.Items.Where(x => x.Profile.Address.City == "Seattle")

// ✅ Supported: translates to WHERE "Tags"[0] = 'featured'
context.Items.Where(x => x.Tags[0] == "featured")

// ⚠️ Client-side only: fetches full "Profile" attribute, extracts City in memory
context.Items.Select(x => new { x.Pk, x.Profile.Address.City })
```

### Owned types and Include (not applicable)
Owned types are always included in the root entity query; explicit `.Include()` is not needed
(and will be ignored).

For complete owned type behavior and examples, see [Owned Types](owned-types.md).
