# ADR-002: Row limiting and paging semantics for DynamoDB queries

## Status

- Proposed
- **Date:** 2026-03-20
- **Deciders:** EntityFrameworkCore.DynamoDb maintainers

---

## Context

The current provider mixes two different ideas:

- **row limiting**: `Take`, `First*`, `Last*`
- **request tuning**: `WithPageSize`, `DefaultPageSize`, `WithoutPagination`

That is problematic because DynamoDB `ExecuteStatementRequest.Limit` is not SQL `LIMIT`. It caps how
many items DynamoDB evaluates in a request, not how many rows the query logically returns.

This creates a fundamental semantic mismatch:

- `Take(n)` in EF Core means "return n rows." Standard LINQ contract.
- DynamoDB `Limit` means "evaluate n items." Items that do not match a non-key filter are evaluated
  but not returned.

When `Take(n)` maps to `Limit = n` in DynamoDB, a query with a non-key filter silently returns
fewer than `n` rows even when more matching rows exist later in the partition. The caller has no
way to distinguish "fewer rows because none exist" from "fewer rows because the evaluation budget
ran out early."

Additionally, the current `WithPageSize` / `DefaultPageSize` API exposes DynamoDB's internal
evaluation-limit concept as a per-request tuning knob. This belongs to the future explicit paging
API (Option C), not the LINQ surface. These should be removed, not renamed.

The provider is not published yet, so breaking changes are acceptable.

## Options Considered

### Option A: Rename only

Keep the current behavior and only rename `WithPageSize` / `DefaultPageSize` to something more
honest, such as `WithEvaluationLimit` / `DefaultEvaluationLimit`.

**Pros:**

- Smallest change.
- Better naming.

**Cons:**

- Keeps implicit provider-side row limiting.
- Does not solve the semantic mismatch between EF Core's `Take` contract and DynamoDB's evaluation
  budget.
- Does not solve the paging model.

### Option B: Remove `Take`, introduce `Limit(n)`, strict by default

Remove `Take` entirely. Replace it with a new provider-specific `Limit(n)` method that explicitly
maps to `ExecuteStatementRequest.Limit`. The name encodes what DynamoDB actually does: evaluate
`n` items, apply filters, return 0..n results. `Limit(n)` must be a positive integer; values of 0
or less throw `ArgumentOutOfRangeException` at query construction time.

Under this model:

- **`Limit(n)`** means "evaluate n items, apply any non-key filter, return whatever matches." A
  single request. No paging. The result count is 0..n. This is the correct mental model for
  DynamoDB's evaluation budget. It works on any query shape — safe path or scan-like — because
  the cost is always bounded to one request.
- **`First*` / `Last*`** are restricted to key-only queries by default. When non-key filters are
  present, they require explicit opt-in via `WithNonKeyFilter()`. `WithNonKeyFilter()` is a
  **permission flag** — it removes the translation restriction that prevents non-key predicates
  in `First*`/`Last*` queries. It does not change execution behavior: evaluation budget comes from
  `Limit(n)` if specified, or DynamoDB's default (1MB) otherwise. Single request. No paging.
  The caller accepts that a match may not be found if the filter is sparse and the evaluation
  budget is small.
- **`WithNonKeyFilter()` is only required for `First*` / `Last*`.** `ToListAsync()` and other
  multi-result terminals work with non-key filters freely and need no opt-in.
- **`First*` / `Last*` on scan-like shapes** (no PK equality) are a translation failure. A full
  table scan to find the first match is not a supported access pattern.
- **`Last*`** is not supported in this iteration. Enabling it requires implementing reverse
  traversal and is deferred to follow-on work.

Remove the evaluation-limit tuning API entirely. `WithPageSize` / `DefaultPageSize` are removed.
The per-page-budget-with-paging use case ("`WithPageSize(n)` + `ToListAsync()`") is explicitly
deferred to Option C's `ToDynamoPageAsync(evaluationLimit: n, ...)` API. For collecting all
results, the provider uses DynamoDB's 1MB default per page, which minimises round trips.

Example of a safe key-only query with evaluation budget:

```csharp
// Evaluate 10 items, all match (key-only), return 10.
var orders = await db.Orders
    .Where(x => x.UserId == userId && x.OrderId >= "2024")
    .Limit(10)
    .ToListAsync(cancellationToken);
```

Example of a scan-like query with evaluation budget:

```csharp
// Evaluate 25 items from a full scan, apply IsActive filter, return 0..25.
var orders = await db.Orders
    .Where(x => x.IsActive)
    .Limit(25)
    .ToListAsync(cancellationToken);
```

Example of `First*` — key-only, no opt-in needed:

```csharp
// Natural key order, single result, no opt-in needed.
var latest = await db.Orders
    .Where(x => x.UserId == userId)
    .OrderByDescending(x => x.OrderId)
    .FirstOrDefaultAsync(cancellationToken);
```

Example of `First*` with non-key filter — explicit opt-in:

```csharp
// WithNonKeyFilter() permits the non-key predicate. Limit(50) sets evaluation budget.
// Single request. Returns first match within the evaluated range or null.
var active = await db.Orders
    .Where(x => x.UserId == userId && x.IsActive)
    .WithNonKeyFilter()
    .Limit(50)
    .FirstOrDefaultAsync(cancellationToken);
```

Example of `First*` with non-key filter, no explicit budget:

```csharp
// WithNonKeyFilter() permits the non-key predicate.
// No Limit(n) — DynamoDB evaluates up to its 1MB default. Single request.
var active = await db.Orders
    .Where(x => x.UserId == userId && x.IsActive)
    .WithNonKeyFilter()
    .FirstOrDefaultAsync(cancellationToken);
```

**Pros:**

- Eliminates the EF Core `Take` vs DynamoDB `Limit` semantic mismatch entirely.
- `Limit(n)` name encodes exactly what DynamoDB does — no ambiguity.
- `First*` stay correct for key-only queries with no special opt-in.
- Non-key filter permission is always explicit via `WithNonKeyFilter()`, never implicit.
- No `WithBestEffortRowLimiting()` needed.

**Cons:**

- `Take` is removed entirely — callers must migrate to `Limit(n)`.
- `WithPageSize` / `DefaultPageSize` and `WithoutPagination` are removed.
- Requires clear diagnostics, migration guidance, and documentation.

### Option C: Explicit paging APIs later

If the provider exposes continuation paging, do it with explicit APIs rather than via normal LINQ.

Possible future shapes:

```csharp
var page = await db.Orders
    .Where(x => x.TenantId == tenantId)
    .OrderBy(x => x.CreatedAt)
    .ToPageAsync(pageSize: 20, continuationToken: token, cancellationToken);
```

```csharp
var page = await db.Orders
    .Where(x => x.TenantId == tenantId)
    .ToDynamoPageAsync(evaluationLimit: 20, continuationToken: token, cancellationToken);
```

Keyset paging is also a strong future option for ordered key queries. This is also where the
per-page evaluation budget (the old `WithPageSize` use case) belongs.

If this API is added, continuation must use an opaque provider cursor contract rather than exposing
raw backend tokens.

## Decision

We will adopt **Option B** as the core model, and keep **Option C** for later.

### What we are doing now

1. Remove `Take` entirely. It has the wrong semantics for DynamoDB — EF Core's `Take(n)` contract
   promises n rows returned, but DynamoDB's `Limit` caps items evaluated. The gap causes silent
   correctness problems with non-key filters.
2. Remove `WithPageSize`, `DefaultPageSize`, and the `FirstAsync(pageSize, ...)` /
   `FirstOrDefaultAsync(pageSize, ...)` convenience overloads entirely. The per-page-budget-with-
   paging use case is deferred to Option C; using DynamoDB's 1MB default is the correct choice for
   `ToListAsync()` paths (fewer round trips).
3. Remove `WithBestEffortRowLimiting()` entirely. It existed solely to make `Take` work on
   scan-like shapes. With `Take` gone, `Limit(n)` handles bounded scan-like evaluation without
   opt-in.
4. Remove `WithoutPagination()`. Its single-page use case is now expressed via `Limit(n)` directly.
5. Introduce `Limit(n)` as a provider-specific operator that maps directly to
   `ExecuteStatementRequest.Limit`. Evaluates `n` items, applies any filters, returns 0..n. Single
   request. No paging. Works on any query shape. `n` must be positive; 0 or negative throws
   `ArgumentOutOfRangeException` at query construction time.
6. Restrict `First*` to key-only queries by default. If a non-key filter is present, translation
   fails unless the caller opts in via `WithNonKeyFilter()`.
7. `WithNonKeyFilter()` is a permission flag. It removes the translation restriction that blocks
   non-key predicates in `First*` queries. Execution is unchanged: evaluation budget is `n` from
   `Limit(n)` if present, or DynamoDB's 1MB default otherwise. Single request. No paging.
   `WithNonKeyFilter()` applied to a key-only query (no non-key predicates) is silently accepted
   as a no-op.
8. `WithNonKeyFilter()` is only required for `First*`. `ToListAsync()` and other multi-result
   terminals accept non-key filters without any opt-in.
9. `First*` on scan-like shapes (no PK equality) is a translation failure with no opt-in path.
   Use `Limit(n)` or `ToListAsync()` instead.
10. `Last*` is not supported in this iteration. Translation always fails. Enabling it requires
    implementing reverse traversal (equivalent to `ScanIndexForward = false`) and is deferred to
    follow-on work.
11. Honor explicit ordering when provided, and do not inject ordering when it is not provided.
12. Support `First*` on key-only queries without explicit `OrderBy`; they operate on the
    backend-returned order only when the selected access path has a reliable natural order (query +
    sort key). The provider sets `Limit = 1` in the request for key-only `First*` queries — every
    evaluated item is guaranteed to match.

### Row-limiting shapes reference

| Shape | API | Behaviour |
|---|---|---|
| Any query with evaluation budget | `.Limit(n)` | Evaluate n items, filter, return 0..n. Single request. |
| `First*`, key-only | `First*` | Provider sets `Limit = 1`. Key order. Single result. |
| `First*`, non-key filter, safe path | `First*` + `.WithNonKeyFilter()` | Non-key predicate permitted. Budget from `Limit(n)` or 1MB default. Single request. |
| `First*`, non-key filter, safe path + explicit budget | `First*` + `.WithNonKeyFilter()` + `.Limit(n)` | Non-key predicate permitted. Evaluate n items. Single request. |
| `First*`, scan-like (no PK equality) | — | Translation failure |
| `Last*` | — | Not supported in this iteration |

### Effective request-limit rule

The provider manages `ExecuteStatementRequest.Limit` internally based on query shape. There is no
user-facing evaluation-limit API.

**`Limit(n)` queries (any shape):**

Exactly one request is sent with `Limit = n`. Returning fewer than `n` results is correct — it
means the filter eliminated some evaluated items, or fewer than `n` items exist in the evaluated
range. There is no paging.

**Key-only `First*`:**

`Limit` is set to `1`. Every item DynamoDB evaluates is guaranteed to match — result count is
0 or 1.

**`First*` with `WithNonKeyFilter()`:**

`Limit` is set from `Limit(n)` if present; otherwise left unset (DynamoDB 1MB default). Single
request. No paging. If no match is found within the evaluated range, null/default is returned.
The caller accepts this trade-off when opting in via `WithNonKeyFilter()`.

**`ToListAsync()` (any shape):**

`Limit` is left unset. The provider pages automatically until `NextToken` is absent, collecting
all matching results. Per-request evaluation granularity is managed internally (DynamoDB 1MB
default). Fine-grained per-page control is deferred to Option C.

### Safe path definition

A query shape is **safe** for `First*` without opt-in when the following holds:

1. The WHERE clause contains an equality condition on the partition key of the accessed source
   (base table, GSI, or LSI).
2. The WHERE clause contains **only key predicates** (partition key and/or sort key conditions).
   No non-key attribute filters.

**Special case — no sort key:** When the accessed source has no sort key, each partition contains
at most one item. In that case `First*` on PK equality is always a point lookup and is safe
regardless of whether non-key filters are present. `WithNonKeyFilter()` is still silently accepted
but not required.

When condition 2 fails (non-key filter present on a source with a sort key), `First*` requires
`WithNonKeyFilter()` to proceed.

When condition 1 fails (no PK equality — scan-like), `First*` is a translation failure with no
opt-in path. Use `Limit(n)` for bounded evaluation on scan-like shapes, or `ToListAsync()` for
full traversal.

For GSIs: the GSI's own partition key must be equality-filtered. An LSI inherits the table
partition key, so condition 1 is satisfied when the table partition key is equality-filtered.

The ordering guarantee on safe paths holds because the provider enforces at translation time that
`OrderBy` / `OrderByDescending` may only reference key attributes (partition key or sort key) of
the active query source. Ordering by non-key attributes is rejected as a translation error
regardless of path safety, since DynamoDB cannot globally order by arbitrary attributes across
pages.

### What we are not doing now

- We are not keeping `Take`. It is removed because it has the wrong semantics for DynamoDB.
- We are not keeping `WithBestEffortRowLimiting()`. It existed solely to make `Take` work on
  scan-like shapes. With `Take` gone, `Limit(n)` handles bounded scan-like evaluation without
  opt-in.
- We are not exposing a per-page evaluation-limit API on the LINQ surface. The per-page-budget-
  with-paging use case (`WithPageSize`) is deferred to Option C.
- We are not implementing `Last*` in this iteration.
- We are not injecting a default ordering when the user did not request one.
- We are not promising scan order or partition-key-only global order as a strict contract.
- We are not exposing continuation paging through normal LINQ operators.

### Follow-on work

- Implement `Last*` for well-ordered paths. Requires reverse traversal equivalent to DynamoDB's
  `ScanIndexForward = false`. Until implemented, `Last*` always fails translation.
- Define the exact safe-query translation error messages for `Limit(n)`, `First*`, and `Last*`.
- Add diagnostics for unsupported row-limiting shapes (e.g. `First*` on scan-like paths).
- Emit a diagnostic warning for `WithNonKeyFilter()` when no match is found and the evaluation
  budget was exhausted (i.e. the result was null due to budget, not absence of data).
- Design explicit paging APIs later if needed (Option C), including the per-page evaluation budget
  use case previously served by `WithPageSize`.
- Remove the following infrastructure coupled to the old model:
  - `DynamoDbOptionsExtension.DefaultPageSize` and `DynamoDbContextOptionsBuilder.DefaultPageSize()`
  - `DynamoQueryCompilationContext`: `PageSizeOverride`, `PageSizeOverrideExpression`, `SinglePageOnly`
  - `SelectExpression`: `PageSize`, `PageSizeExpression`, `ApplyPageSize()` — replaced by
    `Limit` / `LimitExpression` for the new `Limit(n)` operator
  - `SelectExpression`: `ResultLimit`, `ResultLimitExpression`, `ApplyOrCombineResultLimitExpression()`
    — removed with `Take`
  - `QueryingEnumerable`: `_resultLimit` counter and `_pageSize` field
  - `DynamoClientWrapper.ExecutePartiQl`: `singlePageOnly` parameter — tied to `WithoutPagination()`

### Continuation state and cursor contract

- `Limit(n)` queries send a single request and have no continuation state.
- `First*` with or without `WithNonKeyFilter()` sends a single request and has no continuation
  state exposed to the caller.
- `ToListAsync()` manages continuation internally via `NextToken` and does not expose it.
- If/when explicit paging APIs are introduced, the provider returns an opaque cursor token.
- The opaque cursor encapsulates backend continuation state (currently `NextToken` for PartiQL
  `ExecuteStatement`; potentially `LastEvaluatedKey` / `ExclusiveStartKey` if low-level query paths
  are introduced later).
- Raw backend continuation values are not the public API contract.

## Rationale

This keeps the model clear:

- **`Limit(n)`** describes an evaluation budget — exactly what DynamoDB's `Limit` parameter does.
  The name makes the DynamoDB semantic explicit rather than hiding it behind EF Core's `Take`
  contract. Because it is always a single request, it needs no opt-in on any query shape.
- **`First*`** operates on key order for key-only queries, where results are globally ordered and
  every evaluated item matches. Non-key filter permission is always explicit via `WithNonKeyFilter()`
  — it unlocks the non-key predicate but does not change execution behavior.
- **`WithBestEffortRowLimiting()`** is retired because the problem it solved only existed because
  `Take(n)` had the wrong semantics.
- **`WithPageSize`** is retired because per-page evaluation granularity belongs to Option C's
  explicit paging API, not the LINQ surface. The 1MB default is correct for `ToListAsync()`.
- **Request sizing** is managed internally by the provider, not exposed to callers.
- **Paging** stays separate and can be designed like Cosmos `ToPageAsync(...)` later.
- **Continuation state** is wrapped behind an opaque provider cursor for explicit paging APIs.

This also fits what we learned from other providers:

- Cosmos keeps row limiting separate from explicit paging.
- Mongo relies on backend query semantics and does not expose provider paging for normal LINQ.

For DynamoDB, keeping these concepts separate matters even more because evaluated item count and
returned row count can diverge by design. Removing `Take` eliminates the divergence entirely for
the common case rather than papering over it with opt-ins.

For ordering specifically, strict assumptions are limited to queryable access paths with sort keys:

- low-level DynamoDB query traversal is key-ordered,
- direction is controlled by forward/reverse traversal,
- scan-like paths do not provide a strict ordering guarantee for LINQ semantics.

## Consequences

**Positive:**

- Eliminates the EF Core `Take` vs DynamoDB evaluation-budget mismatch.
- `Limit(n)` name encodes exactly what happens — no hidden semantics.
- Strict-by-default LINQ behavior for `First*`.
- Non-key filter permission is always explicit via `WithNonKeyFilter()`.
- No `WithBestEffortRowLimiting()` or `WithPageSize` to explain — the opt-in surface is small and
  focused.
- Clean path to future paging APIs.

**Trade-offs:**

- `Take` is removed with no direct replacement on the LINQ surface. Callers who need a row count
  guarantee must wait for the explicit paging API (Option C).
- `WithPageSize` / `DefaultPageSize`, their convenience overloads, `WithBestEffortRowLimiting()`,
  and `WithoutPagination()` are all removed. Users who need per-request evaluation granularity
  must use the explicit paging API (Option C) or the AWS SDK directly.
- `Last*` is not supported in this iteration.
- More diagnostics and documentation work are required.

## References

- AWS ExecuteStatement API:
  <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_ExecuteStatement.html>
- DynamoDB PartiQL SELECT:
  <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.select.html>
- Repository code:
  `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoDbQueryableExtensions.cs`,
  `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`,
  `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.QueryingEnumerable.cs`,
  `src/EntityFrameworkCore.DynamoDb/Storage/DynamoClientWrapper.cs`
