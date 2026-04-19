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
- `StartsWith`
- `Limit(n)` (evaluation budget)
- `First` / `FirstOrDefault` (key-only safe path)
- `WithIndex` / `WithoutIndex`

### Predicate operators

- `!` (logical NOT) translates to PartiQL `NOT (expr)`
- `== null` / `!= null` translates to `IS NULL OR IS MISSING` / `IS NOT NULL AND IS NOT MISSING`
- `EF.Functions.IsNull(prop)`, `IsNotNull`, `IsMissing`, `IsNotMissing` for explicit per-predicate control
- `prop >= a && prop <= b` (inclusive range) translates to `prop BETWEEN a AND b`

### Not supported today

- `Take` — use `.Limit(n)` for evaluation budget
- `Any` / `All`
- `Single` / `SingleOrDefault`
- `Count`, `LongCount`, `Sum`, `Average`, `Min`, `Max`
- `Skip`
- `GroupBy`
- `Join` / `GroupJoin` / `SelectMany` / `LeftJoin` / `RightJoin`
- `Union` / `Concat` / `Except` / `Intersect`
- `Distinct`, `Reverse`, `Last` / `LastOrDefault`
- Method calls in predicates except supported `Contains` and `StartsWith` patterns

### Server-side only policy

- This provider only supports query shapes that can be translated to DynamoDB PartiQL.
- Unsupported operators fail translation with a detailed `InvalidOperationException` message.
- The provider does not silently switch unsupported LINQ operators to client-side evaluation.
- Some projection shaping in `Select` may still execute client-side after server-side query results are returned.

## Operator matrix (current contract)

| Operator                                              | Server translation                                | Client behavior                               | Notes                                                                                                 |
| ----------------------------------------------------- | ------------------------------------------------- | --------------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| `Where`                                               | PartiQL `WHERE`                                   | N/A                                           | Boolean members normalize to `= TRUE`                                                                 |
| `!` (logical NOT)                                     | PartiQL `NOT (expr)`                              | N/A                                           | Operand is always parenthesised                                                                       |
| `== null` (constant)                                  | `IS NULL OR IS MISSING`                           | N/A                                           | Covers both DynamoDB null representations                                                             |
| `!= null` (constant)                                  | `IS NOT NULL AND IS NOT MISSING`                  | N/A                                           | De Morgan inverse of `== null`                                                                        |
| `EF.Functions.IsNull(prop)`                           | `IS NULL`                                         | N/A                                           | Explicit: NULL type only                                                                              |
| `EF.Functions.IsNotNull(prop)`                        | `IS NOT NULL`                                     | N/A                                           | Explicit: not NULL type                                                                               |
| `EF.Functions.IsMissing(prop)`                        | `IS MISSING`                                      | N/A                                           | Explicit: absent attribute                                                                            |
| `EF.Functions.IsNotMissing(prop)`                     | `IS NOT MISSING`                                  | N/A                                           | Explicit: attribute present                                                                           |
| `prop >= a && prop <= b`                              | PartiQL `prop BETWEEN a AND b`                    | N/A                                           | Both bounds inclusive; mixed bounds (`>` + `<=`) fall back to two comparisons                         |
| `Contains`                                            | PartiQL `contains(...)` or `IN [ ... ]`           | N/A                                           | Only `string.Contains(string)` and in-memory collection membership are supported                      |
| `string.StartsWith(string)`                           | `begins_with(attr, <prefix>)`                     | N/A                                           | Captured values are parameterized; inline literals may be inlined; other overloads are not translated |
| `Select`                                              | Explicit projection list                          | Some computed projections can run client-side | No `SELECT *`                                                                                         |
| Nested owned property path (`x.Profile.Address.City`) | PartiQL dot-notation `"Profile"."Address"."City"` | N/A                                           | Supported in `Where` predicates only; not supported in `Select` projections                           |
| List index access (`x.Tags[0]`)                       | PartiQL bracket-notation `"Tags"[0]`              | N/A                                           | Supported in `Where` predicates only; index must be a compile-time constant                           |
| `OrderBy` / `ThenBy`                                  | PartiQL `ORDER BY`                                | N/A                                           | Precedence and parentheses preserved                                                                  |
| `Limit(n)`                                            | Sets `ExecuteStatementRequest.Limit = n`          | Single request, 0..n results                  | Last call wins; must be positive                                                                      |
| `First*` (key-only, no explicit limit)                | Sets implicit `Limit=1`; single request           | N/A                                           | Safe path only; unsafe paths throw — use `AsAsyncEnumerable()`                                        |
| `First*` + `Limit(n)`                                 | Translation failure                               | —                                             | Use `.AsAsyncEnumerable().FirstOrDefaultAsync(ct)` (optional `Limit(n)` only if you want a budget)    |
| `First*` on non-key/scan-like path                    | Translation failure                               | —                                             | Use `.AsAsyncEnumerable().FirstOrDefaultAsync(ct)` (add `Limit(n)` only for bounded sampling)         |
| `AsAsyncEnumerable()` + `First*`                      | Optional `Limit(n)` server-side, then client-side | Takes first from result set                   | Standard EF Core explicit client-side evaluation                                                      |
| `WithIndex(name)`                                     | Sets query source to `"Table"."Index"`            | N/A                                           | Name must resolve to an index on the queried entity type or its base types                            |
| `WithoutIndex()`                                      | Suppresses index selection                        | N/A                                           | Forces base-table execution and logs `DYNAMO_IDX006`; cannot be combined with `WithIndex(...)`        |

## Scalar property types

The following CLR scalar types are supported as entity properties. Types without a native DynamoDB
wire type are mapped through EF Core's built-in value converters.

| CLR type                                    | DynamoDB wire type | Wire format / notes                                |
| ------------------------------------------- | ------------------ | -------------------------------------------------- |
| `string`                                    | `S`                | —                                                  |
| `bool`                                      | `BOOL`             | —                                                  |
| `int`, `long`, `float`, `double`, `decimal` | `N`                | Numeric string                                     |
| `ushort`, `uint`, `ulong`                   | `N`                | Numeric string                                     |
| `byte[]`                                    | `B`                | Binary                                             |
| `Guid`                                      | `S`                | `"D"` format, e.g. `"550e8400-e29b-41d4-a716-..."` |
| `DateTime`                                  | `S`                | ISO 8601 round-trip (`"O"`)                        |
| `DateTimeOffset`                            | `S`                | ISO 8601 round-trip (`"O"`)                        |
| `DateOnly`                                  | `S`                | `"yyyy-MM-dd"`, e.g. `"2026-04-19"`                |
| `TimeOnly`                                  | `S`                | `"HH:mm:ss"` (whole-second) or `"o"` (sub-second)  |
| `TimeSpan`                                  | `S`                | Constant (`"c"`) format, e.g. `"01:30:00"`         |
| Enum                                        | `S`                | Name string by default                             |

Nullable variants of all types above are supported. Custom types can be mapped via EF Core
[value converters](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions).

## General paging model

DynamoDB `ExecuteStatementRequest.Limit` controls evaluation (how many items are read), not the
number of matching rows returned. A page can return zero matches and still include `NextToken`
when more items could match.

- **`Limit(n)` + `ToListAsync()`**: single request, evaluates at most `n` items. No paging.
- **`Limit(n)` + `First*`**: translation failure — use `.AsAsyncEnumerable().FirstOrDefaultAsync(ct)`.
- **`First*` key-only (no explicit limit)**: single request, implicit `Limit=1`.
- **`First*` on non-key/scan-like path**: translation failure — use `.AsAsyncEnumerable().FirstOrDefaultAsync(ct)`.
- **`ToListAsync()` (no limit)**: multi-page, `Limit=null` per request. Provider follows `NextToken`.

The provider never emits SQL `LIMIT`; the limit is a request-level field on `ExecuteStatementRequest`.

## DynamoDB PartiQL context (background)

- PartiQL `SELECT` can behave like a scan unless the `WHERE` clause uses partition-key equality or
    partition-key `IN`.
- DynamoDB PartiQL supports operators such as `BETWEEN` (inclusive) and `IN`. The provider
    translates `BETWEEN` from a matching `>=` + `<=` LINQ pattern on the same property.
- DynamoDB documents `IN` limits as up to 50 hash-key values or up to 100 non-key values.
- Query predicates do not imply GSI/LSI targeting. The current provider analyzes and executes
    queries against the modeled table key unless and until explicit index-aware execution support is
    added.
- Access-pattern guidance in this document therefore applies to the modeled DynamoDB table
    partition/sort key, not to EF `HasKey(...)` or to un-targeted secondary indexes.

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
    `Where`, `Select`, `OrderBy`/`ThenBy`, `Limit(n)`, and `First`/`FirstOrDefault`.
- `First*` on derived/shared-table queries requires a single-item base-table lookup before
    discriminator filtering (PK-only on PK-only table, or PK+SK equality on PK+SK table). Derived
    PK-only queries on PK+SK tables throw translation failure; use
    `.AsAsyncEnumerable().FirstOrDefaultAsync(ct)`.
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

| State     | DynamoDB storage                              | PartiQL predicate |
| --------- | --------------------------------------------- | ----------------- |
| NULL type | Attribute present with `{ NULL: true }` value | `attr IS NULL`    |
| MISSING   | Attribute key absent from the item entirely   | `attr IS MISSING` |

EF Core users should not need to distinguish which representation was used. The provider
therefore maps `== null` to cover both:

| LINQ expression                     | PartiQL emitted                                |
| ----------------------------------- | ---------------------------------------------- |
| `x.Prop == null`                    | `"Prop" IS NULL OR "Prop" IS MISSING`          |
| `x.Prop != null`                    | `"Prop" IS NOT NULL AND "Prop" IS NOT MISSING` |
| `EF.Functions.IsNull(x.Prop)`       | `"Prop" IS NULL`                               |
| `EF.Functions.IsNotNull(x.Prop)`    | `"Prop" IS NOT NULL`                           |
| `EF.Functions.IsMissing(x.Prop)`    | `"Prop" IS MISSING`                            |
| `EF.Functions.IsNotMissing(x.Prop)` | `"Prop" IS NOT MISSING`                        |

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

## StartsWith

**Purpose**

- Test whether a string attribute begins with a given prefix.

**Translation**

- `entity.Name.StartsWith("prefix")` translates to `begins_with("Name", ?)` when the prefix is captured/parameterized.
- Inline constants can be emitted directly as literals, for example `begins_with("Name", 'prefix')`.

```csharp
// Captured variable
var prefix = "ORDER#";
db.Items.Where(x => x.Sk.StartsWith(prefix));
// WHERE begins_with("Sk", ?)

// Inline literal
db.Items.Where(x => x.Sk.StartsWith("ORDER#"));
// WHERE begins_with("Sk", 'ORDER#')
```

**Limitations / DynamoDB quirks**

- Only `string.StartsWith(string)` is supported; `char`, `StringComparison`, and culture/ignore-case overloads are not translated and will throw.
- `begins_with` performs a case-sensitive, literal prefix check — no wildcards, no escaping required.
- DynamoDB `begins_with` only accepts string attributes; numeric or binary attributes will not match.
- See [DynamoDB begins_with documentation](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-functions.beginswith.html).

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

## BETWEEN

**Purpose**

- Test whether a property value falls within an inclusive range.

**Translation**

- A LINQ predicate of the form `x.Prop >= low && x.Prop <= high` is rewritten to
    PartiQL `"Prop" BETWEEN ? AND ?`.
- Both bounds must be inclusive (`>=` and `<=`). If either bound is exclusive (`>` or `<`),
    the predicate is kept as two separate comparisons.

```csharp
// Inclusive range → BETWEEN
db.Orders.Where(o => o.Total >= 10m && o.Total <= 100m)
// SELECT ... FROM "Orders" WHERE "Total" BETWEEN ? AND ?

// Mixed bounds → two comparisons (no BETWEEN rewrite)
db.Orders.Where(o => o.Total > 10m && o.Total <= 100m)
// SELECT ... FROM "Orders" WHERE "Total" > ? AND "Total" <= ?
```

**Sort key range queries**

DynamoDB allows only a single condition on the sort key per query. `BETWEEN` satisfies this as
one inclusive range condition. Using `>=` and `<=` without the BETWEEN rewrite would produce two
separate sort-key comparisons, which DynamoDB cannot use as a key condition and falls back to a
scan. To ensure the BETWEEN rewrite fires for sort key ranges:

- Use `>=` for the lower bound and `<=` for the upper bound on the **same property**.
- Write the lower bound first: `sk >= low && sk <= high` (either left/right ordering of the
    conditions is accepted, but the bound values are not reordered).

```csharp
var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
var to   = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);

// ✅ Triggers BETWEEN — property on the left of each comparison, both bounds inclusive
db.Orders
    .Where(o => o.Pk == "CUSTOMER#42" && o.CreatedAt >= from && o.CreatedAt <= to)
    .ToList();
// WHERE "Pk" = ? AND "CreatedAt" BETWEEN ? AND ?

// ❌ Does NOT trigger BETWEEN — property must be on the LEFT of each comparison operator.
//    Swapping to "from <= o.CreatedAt" places the property on the right, so the rewrite
//    does not fire and two separate sort-key comparisons are emitted instead.
db.Orders
    .Where(o => o.Pk == "CUSTOMER#42" && from <= o.CreatedAt && to >= o.CreatedAt)
    .ToList();
// WHERE "Pk" = ? AND ? <= "CreatedAt" AND ? >= "CreatedAt"
// (two sort-key conditions → DynamoDB falls back to a scan)

// ❌ Does NOT trigger BETWEEN — exclusive lower bound, emits two comparisons
db.Orders
    .Where(o => o.Pk == "CUSTOMER#42" && o.CreatedAt > from && o.CreatedAt <= to)
    .ToList();
// WHERE "Pk" = ? AND "CreatedAt" > ? AND "CreatedAt" <= ?
// (two sort-key conditions → DynamoDB falls back to a scan)
```

**Bound ordering is not validated**

The provider does not reorder or normalize the bound values. If the bounds are logically
inverted — for example `x.Score >= 500 && x.Score <= 100` — the predicate is still rewritten
to `"Score" BETWEEN 500 AND 100`, which DynamoDB evaluates as an empty range and returns no
results. Ensure `low <= high` at the call site.

**Limitations / DynamoDB quirks**

- Only single-property inclusive ranges trigger the rewrite. Multi-column range expressions
    are emitted as individual comparisons.
- DynamoDB BETWEEN is inclusive on both ends; see
    [PartiQL operators](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-operators.html).

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
- Both `OrderBy`/`ThenBy` (ascending) and `OrderByDescending`/`ThenByDescending` (descending) are
    supported in any combination per key.

**Valid ordering attributes**

- Only partition key and sort key attributes are valid ordering columns. Non-key attributes are
    rejected at query compilation with a provider error.

**Single-partition queries** (`WHERE PK = value`):

- `OrderBy(e => e.Pk)` — order by partition key
- `OrderBy(e => e.Sk)` — order by sort key
- `OrderBy(e => e.Pk).ThenBy(e => e.Sk)` — order by PK then SK
- Any `ASC`/`DESC` combination on the above is valid

**Multi-partition queries** (`WHERE PK IN (...)`):

- The partition key **must lead** the `ORDER BY` chain.
- `OrderBy(e => e.Pk)` — valid
- `OrderBy(e => e.Pk).ThenBy(e => e.Sk)` — valid
- `OrderBy(e => e.Sk)` — **invalid**: partition key must come first
- `OrderByDescending(e => e.Pk).ThenByDescending(e => e.Sk)` — valid (any direction)

```csharp
// Single partition — order by PK or SK
db.Orders
    .Where(o => o.Pk == "CUSTOMER#42")
    .OrderBy(o => o.Pk)
    .ThenByDescending(o => o.Sk)
    .ToList();
// ORDER BY "Pk" ASC, "Sk" DESC

// Multi-partition — PK must lead
var customers = new[] { "CUSTOMER#1", "CUSTOMER#2" };
db.Orders
    .Where(o => customers.Contains(o.Pk))
    .OrderBy(o => o.Pk)
    .ThenBy(o => o.Sk)
    .ToList();
// ORDER BY "Pk" ASC, "Sk" ASC
```

**Limitations / DynamoDB quirks**

- A `WHERE` clause with an equality or IN constraint on the partition key is required; `ORDER BY`
    without a partition key constraint throws a provider error.
- For multi-partition queries, the partition key must be the first `ORDER BY` column; ordering by
    sort key first is not supported.
- Non-key attributes in `ORDER BY` always produce a provider error.
- See [DynamoDB PartiQL SELECT](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.select.html) for engine-level ordering behavior.

## Limit(n)

**Purpose**

- Set a DynamoDB evaluation budget for a query.

**Translation**

- Sets `ExecuteStatementRequest.Limit = n`. Always a single request.
- DynamoDB evaluates at most `n` items, applies any non-key filters, and returns 0..n results.
- When chained multiple times, the last call wins.
- `n` must be positive. Zero or negative throws `ArgumentOutOfRangeException` at construction time
    for constants, or at execution time for runtime values.

**Example**

```csharp
// Evaluate 25 items, apply filter, return 0..25.
var results = await db.Orders
    .Where(x => x.IsActive)
    .Limit(25)
    .ToListAsync(cancellationToken);

// Chaining — last wins (effective limit: 20).
await db.Orders.Limit(10).Limit(20).ToListAsync(cancellationToken);
```

**Compiled queries with runtime parameters**

```csharp
var query = EF.CompileAsyncQuery((OrderDbContext ctx, int n)
    => ctx.Orders.Limit(n));

var results = await query(db, 50).ToListAsync(cancellationToken);
```

**Limitations**

- Does not guarantee `n` rows returned. DynamoDB reads `n` items, applies filters, and returns
    whatever matches in that range. Use `ToListAsync()` without `Limit(n)` to collect all matches.
- There is no paging. If fewer than `n` matching items exist in the evaluated range, the result is
    simply shorter.

## First / FirstOrDefault

**Purpose**

- Return the first result (throw if none exist for `First`; return `null` for `FirstOrDefault`).

**Translation**

`First*` works server-side **only** on the safe key-only path. All other shapes throw at
translation time — use `.AsAsyncEnumerable()` to cross into client-side LINQ explicitly.

| Shape                                                      | Limit on request    | Notes                                                       |
| ---------------------------------------------------------- | ------------------- | ----------------------------------------------------------- |
| Key-only (PK equality, key-only predicates, no user limit) | `1` (implicit)      | Safe path — single request                                  |
| `Limit(n)` + `First*`                                      | Translation failure | Use `.Limit(n).AsAsyncEnumerable().FirstOrDefaultAsync(ct)` |
| Non-key predicate or scan-like                             | Translation failure | Use `.Limit(n).AsAsyncEnumerable().FirstOrDefaultAsync(ct)` |

`First*` on the safe path is always a **single request**. The provider never pages for a `First*` terminal.

**Safe path conditions** — ALL must hold:

1. No user-specified `Limit(n)`.
1. The `WHERE` clause contains a partition-key equality condition.
1. The `WHERE` clause contains only key predicates (no non-key attribute filters).

**Special case — no sort key**: When the queried source has no sort key, each partition contains
at most one item. `First*` with PK equality is always safe regardless of non-key predicates
(condition 3 is relaxed).

**Example — key-only (safe)**

```csharp
// Safe path: PK + SK equality. Uses Limit=1 automatically.
var item = await db.Orders
    .Where(o => o.UserId == userId && o.OrderId == orderId)
    .FirstOrDefaultAsync(cancellationToken);
```

**Example — non-key filter (use AsAsyncEnumerable)**

```csharp
// Non-key predicate: IsActive is not PK or SK.
// Fetch up to 50 items server-side, take the first match client-side.
var active = await db.Orders
    .Where(o => o.UserId == userId && o.IsActive)
    .Limit(50)
    .AsAsyncEnumerable()
    .FirstOrDefaultAsync(cancellationToken);
```

**Limitations / DynamoDB quirks**

- Unsafe `First*` always throws at translation time — never silently returns null.
- `Limit(n) + First*` always throws. `Limit(n)` is an evaluation budget that may return multiple
    items; combining it directly with `First*` is ambiguous. Use `AsAsyncEnumerable()` to make the
    client-side selection explicit.
- The `AsAsyncEnumerable()` pattern is the standard EF Core approach for explicit client-side
    evaluation — it marks the boundary between server-side and LINQ-to-objects evaluation.

## External references

- AWS ExecuteStatement API: <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_ExecuteStatement.html>
- DynamoDB PartiQL SELECT: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.select.html>
- DynamoDB PartiQL operators: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-operators.html>
- DynamoDB PartiQL functions: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-functions.html>
- DynamoDB `begins_with`: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-functions.beginswith.html>
- PartiQL identifiers: <https://partiql.org/concepts/identifiers.html>
