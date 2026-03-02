---
icon: lucide/function-square
---

# Query Operators and DynamoDB Behavior

This document is a living reference for how LINQ operators behave in this provider. Each operator
section includes how it translates today and the DynamoDB or provider-specific limitations you
should keep in mind. Add to these sections as support expands.

## At a glance

### Supported today
- `Where`
- `Select`
- `OrderBy` / `OrderByDescending`
- `ThenBy` / `ThenByDescending`
- `Contains` (supported shapes only)
- `Take`
- `First` / `FirstOrDefault`
- `WithPageSize`
- `WithoutPagination`

### Predicate operators
- `!` (logical NOT) translates to PartiQL `NOT (expr)`
- `== null` / `!= null` translates to `IS NULL OR IS MISSING` / `IS NOT NULL AND IS NOT MISSING`
- `EF.Functions.IsNull(prop)`, `IsNotNull`, `IsMissing`, `IsNotMissing` for explicit per-predicate control

### Not supported today
- `Any` / `All`
- `Single` / `SingleOrDefault`
- `Count`, `LongCount`, `Sum`, `Average`, `Min`, `Max`
- `Skip`
- `GroupBy`
- `Join` / `GroupJoin` / `SelectMany` / `LeftJoin` / `RightJoin`
- `Union` / `Concat` / `Except` / `Intersect`
- `Distinct`, `Reverse`, `Last` / `LastOrDefault`
- Method calls in predicates except supported `Contains` patterns

### Server-side only policy
- This provider only supports query shapes that can be translated to DynamoDB PartiQL.
- Unsupported operators fail translation with a detailed `InvalidOperationException` message.
- The provider does not silently switch unsupported LINQ operators to client-side evaluation.
- Some projection shaping in `Select` may still execute client-side after server-side query results are returned.

## Operator matrix (current contract)

| Operator | Server translation | Client behavior | Notes |
| --- | --- | --- | --- |
| `Where` | PartiQL `WHERE` | N/A | Boolean members normalize to `= TRUE` |
| `!` (logical NOT) | PartiQL `NOT (expr)` | N/A | Operand is always parenthesised |
| `== null` (constant) | `IS NULL OR IS MISSING` | N/A | Covers both DynamoDB null representations |
| `!= null` (constant) | `IS NOT NULL AND IS NOT MISSING` | N/A | De Morgan inverse of `== null` |
| `EF.Functions.IsNull(prop)` | `IS NULL` | N/A | Explicit: NULL type only |
| `EF.Functions.IsNotNull(prop)` | `IS NOT NULL` | N/A | Explicit: not NULL type |
| `EF.Functions.IsMissing(prop)` | `IS MISSING` | N/A | Explicit: absent attribute |
| `EF.Functions.IsNotMissing(prop)` | `IS NOT MISSING` | N/A | Explicit: attribute present |
| `Contains` | PartiQL `contains(...)` or `IN [ ... ]` | N/A | Only `string.Contains(string)` and in-memory collection membership are supported |
| `Select` | Explicit projection list | Some computed projections can run client-side | No `SELECT *` |
| `OrderBy` / `ThenBy` | PartiQL `ORDER BY` | N/A | Precedence and parentheses preserved |
| `Take(n)` | Sets result limit expression | Stops after `n` results | Does not emit SQL `LIMIT` |
| `First*` | Sets result limit `1` | Stops after first result | May scan multiple pages unless pagination disabled |
| `WithPageSize(n)` | Sets request `Limit` | N/A | Last call wins |
| `WithoutPagination()` | Single request only | Stops after first page | Can return incomplete results |

## General paging model
- Result limit (how many results are returned) is separate from page size (how many items DynamoDB
  evaluates per request).
- DynamoDB `Limit` controls evaluation, not returned matches. A page can return zero matches and
  still include `NextToken` if more items could match.
- DynamoDB read responses can stop at the request `Limit` or at the 1 MB processed-data cap,
  whichever is reached first.
- The provider does not emit SQL `LIMIT`; it stops after enough results are returned while request
  `Limit` is used as page size.
- The provider logs a warning when a row-limiting query (`First*`, `Take`) runs without a configured
  page size (`WithPageSize` or `DefaultPageSize`).

## DynamoDB PartiQL context (background)
- PartiQL `SELECT` can behave like a scan unless the `WHERE` clause uses partition-key equality or
  partition-key `IN`.
- DynamoDB PartiQL supports operators such as `BETWEEN` (inclusive) and `IN`, but provider
  translation support is narrower than the full DynamoDB operator surface.
- DynamoDB documents `IN` limits as up to 50 hash-key values or up to 100 non-key values.

## Identifier quoting notes
- This provider always quotes identifiers in generated PartiQL.
- Quoted identifiers in PartiQL are case-sensitive.

## Shared-table discriminator behavior
- For shared-table mappings (multiple concrete entity types in one table group), root `DbSet<T>`
  queries include discriminator filtering.
- The discriminator filter is injected at query-root creation and composes with user predicates
  using `AND`.
- For inheritance hierarchies, querying a base type includes all concrete discriminator values in
  that hierarchy.
- For the currently supported operator surface, discriminator filtering therefore applies to:
  `Where`, `Select`, `OrderBy`/`ThenBy`, `Take`, `First`/`FirstOrDefault`, `WithPageSize`, and
  `WithoutPagination`.
- Unsupported operators (joins, groupings, set operations, skip, aggregates, single-result
  operators) are outside the discriminator coverage contract.

Conceptual example for `DbSet<UserEntity>` against a shared table:

```sql
SELECT Pk, Sk, Name, "$type"
FROM "app-table"
WHERE Pk = 'TENANT#U' AND "$type" = 'UserEntity'
```

Conceptual example for `DbSet<PersonEntity>` (base type) in a hierarchy:

```sql
SELECT Pk, Sk, Name, Department, Level, "$type"
FROM "app-table"
WHERE Pk = 'TENANT#H' AND ("$type" = 'EmployeeEntity' OR "$type" = 'ManagerEntity')
```

## Where
**Purpose**
- Filter results by predicate.

**Translation**
- Translated to PartiQL `WHERE`.
- Boolean members are normalized to explicit comparisons (e.g., `IsActive` becomes `IsActive = TRUE`).

**Limitations / DynamoDB quirks**
- Filters may return zero matches on a page even when more matches exist on later pages.

## IS NULL / IS MISSING
**Purpose**
- Test whether a nullable attribute is absent or holds no value.

**Translation**
DynamoDB has two representations of "no value" for an attribute:

| State | DynamoDB storage | PartiQL predicate |
|---|---|---|
| NULL type | Attribute present with `{ NULL: true }` value | `attr IS NULL` |
| MISSING | Attribute key absent from the item entirely | `attr IS MISSING` |

EF Core users should not need to distinguish which representation was used. The provider
therefore maps `== null` to cover both:

| LINQ expression | PartiQL emitted |
|---|---|
| `x.Prop == null` | `"Prop" IS NULL OR "Prop" IS MISSING` |
| `x.Prop != null` | `"Prop" IS NOT NULL AND "Prop" IS NOT MISSING` |
| `EF.Functions.IsNull(x.Prop)` | `"Prop" IS NULL` |
| `EF.Functions.IsNotNull(x.Prop)` | `"Prop" IS NOT NULL` |
| `EF.Functions.IsMissing(x.Prop)` | `"Prop" IS MISSING` |
| `EF.Functions.IsNotMissing(x.Prop)` | `"Prop" IS NOT MISSING` |

The `EF.Functions` methods are for advanced use cases where the NULL vs MISSING distinction
matters (e.g., schema migration, interop with non-EF-written items).

When `== null` is composed with `AND`, the OR sub-expression is automatically parenthesised:
```csharp
// x.Name == null && x.IsActive → ("Name" IS NULL OR "Name" IS MISSING) AND "IsActive" = TRUE
```

**Limitations / DynamoDB quirks**
- Parameterized null (`x.Prop == someVar` where `someVar` is null at runtime) uses
  `= ?` with `AttributeValue { NULL = true }`, which only matches NULL type — not MISSING.
  This is a DynamoDB engine limitation (`IS` does not accept parameters). See
  [Limitations](limitations.md#parameterized-null-inconsistency).
- Two-column nullable comparisons (`x.A == x.B` where both are nullable) are not supported
  and will not produce correct results. See [Limitations](limitations.md#null-column-comparison).

## Not
**Purpose**
- Negate a boolean predicate.

**Translation**
- `!expr` translates to PartiQL `NOT (expr)`.
- The operand is always wrapped in parentheses: `NOT ("IsActive" = TRUE)`.
- Compound operands are parenthesised correctly: `NOT ("IsActive" = TRUE AND "Score" > 0)`.

**Limitations / DynamoDB quirks**
- Only logical negation of boolean search conditions is supported; bitwise complement is not translated.

## Contains
**Purpose**
- Check string substring membership or in-memory collection membership.

**Translation**
- `entity.Name.Contains("Ada")` translates to `contains(Name, ?)`.
- `ids.Contains(entity.Id)` translates to `Id IN [?, ?, ...]`.
- Collection membership placeholders are expanded at runtime based on collection size.

**Limitations / DynamoDB quirks**
- Only `string.Contains(string)` is supported; overloads such as `char` and `StringComparison` are not translated.
- Only in-memory collection membership is supported (for example `ids.Contains(entity.Id)`).
- Collection attribute containment (for example `entity.Tags.Contains("x")`) is not supported.
- Empty collections translate to a constant-false predicate (`1 = 0`).
- DynamoDB `IN` limits apply: up to 50 partition-key values or up to 100 non-key values.
- `NULL` collection elements are passed through as DynamoDB `NULL`; DynamoDB evaluation semantics apply.

## Select
**Purpose**
- Shape the projection.

**Translation**
- Translated to explicit `SELECT <projection>`; no `SELECT *` is emitted.

**Limitations / DynamoDB quirks**
- Projection is explicit to keep attribute reads predictable and aligned with type mapping.

## OrderBy / ThenBy
**Purpose**
- Order results by one or more keys.

**Translation**
- Translated to PartiQL `ORDER BY` with `ASC`/`DESC`.

**Limitations / DynamoDB quirks**
- Ordering semantics depend on DynamoDB access patterns and query shape.

## Take(n)
**Purpose**
- Return at most `n` results.

**Translation**
- Sets a result limit of `n` and stops after returning `n` items.
- Page size comes from `.WithPageSize(...)` or `DefaultPageSize` if configured.
- Composes with other `Take`/`First*` operators using the minimum effective limit.

**Limitations / DynamoDB quirks**
- `n` does not imply DynamoDB request `Limit` is `n`.
- If filters are present, multiple pages may be needed to collect `n` matches.
- Without a configured page size, DynamoDB may return a large first page and the provider will log a warning.

## First
**Purpose**
- Return the first result; throw if none exist.

**Translation**
- Sets a result limit of `1` and stops after returning the first item.
- Page size comes from `.WithPageSize(...)` or `DefaultPageSize` if configured.
- If a prior `Take(n)` is applied, the effective result limit is still `1`.

**Limitations / DynamoDB quirks**
- An empty page does not prove absence if `NextToken` is present.
- Without a configured page size, DynamoDB may return a large first page and the provider will log a warning.

## FirstOrDefault
**Purpose**
- Return the first result or `null` if none exist.

**Translation**
- Sets a result limit of `1` and stops after returning the first item.
- Page size comes from `.WithPageSize(...)` or `DefaultPageSize` if configured.
- If a prior `Take(n)` is applied, the effective result limit is still `1`.

**Limitations / DynamoDB quirks**
- An empty page does not prove absence if `NextToken` is present.
- Without a configured page size, DynamoDB may return a large first page and the provider will log a warning.

## WithPageSize(int)
**Purpose**
- Override request page size for a specific query.

**Translation**
- Sets DynamoDB request `Limit` (items evaluated per request).
- If `WithPageSize` is chained multiple times, the last call wins.

**Example**
```csharp
var results = await db.Items
    .WithPageSize(10)
    .Where(item => item.IsActive)
    .WithPageSize(25)
    .ToListAsync();
// Effective page size: 25
```

**Limitations / DynamoDB quirks**
- Smaller page sizes can increase round trips under filtering.

## WithoutPagination()
**Purpose**
- Force a single request for a query.

**Translation**
- Stops after the first DynamoDB request, even if `NextToken` is returned.

**Limitations / DynamoDB quirks**
- Best-effort mode: may return incomplete results when more matches exist on later pages.

## External references
- AWS ExecuteStatement API: <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_ExecuteStatement.html>
- DynamoDB PartiQL SELECT: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.select.html>
- DynamoDB PartiQL operators: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-operators.html>
- DynamoDB PartiQL functions: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-functions.html>
- PartiQL identifiers: <https://partiql.org/concepts/identifiers.html>
