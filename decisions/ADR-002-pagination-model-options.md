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
- **`First*`** is restricted to key-only queries by default (PK equality, no non-key filters).
  Any `First*` query that goes beyond the safe key-only shape requires explicit opt-in via
  `WithNonKeyFilter()`. This covers: non-key filters on a safe path, non-key filters on a
  scan-like path, and scan-like paths of any kind. The caller accepts the evaluation cost.
- **`WithNonKeyFilter()`** is a **permission flag** — it removes the translation restriction that
  limits `First*` to safe key-only queries. It does not change execution behavior: evaluation
  budget comes from `Limit(n)` if specified, or DynamoDB's default (1MB) otherwise. Single
  request. No paging. Applied to a key-only query with no non-key predicates, it is a silent
  no-op. Applied to a `ToListAsync()` or other multi-result terminal, it is also a silent no-op —
  those terminals always accept non-key filters without opt-in.
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
// Ascending sort key order (DynamoDB forward traversal). Single result.
var earliest = await db.Orders
    .Where(x => x.UserId == userId)
    .FirstOrDefaultAsync(cancellationToken);

// Explicit descending order.
var latest = await db.Orders
    .Where(x => x.UserId == userId)
    .OrderByDescending(x => x.OrderId)
    .FirstOrDefaultAsync(cancellationToken);
```

Example of `First*` with non-key filter on a safe path:

```csharp
// WithNonKeyFilter() permits the non-key predicate. Limit(50) sets evaluation budget.
// Single request. Returns first match within the evaluated range or null.
var active = await db.Orders
    .Where(x => x.UserId == userId && x.IsActive)
    .WithNonKeyFilter()
    .Limit(50)
    .FirstOrDefaultAsync(cancellationToken);
```

Example of `First*` on a scan-like path — caller accepts unbounded cost:

```csharp
// No PK equality — full table scan. WithNonKeyFilter() opts in to this.
// Limit(100) bounds the evaluation to 100 items. Single request.
var first = await db.Orders
    .Where(x => x.IsActive)
    .WithNonKeyFilter()
    .Limit(100)
    .FirstOrDefaultAsync(cancellationToken);
```

**Pros:**

- Eliminates the EF Core `Take` vs DynamoDB `Limit` semantic mismatch entirely.
- `Limit(n)` name encodes exactly what DynamoDB does — no ambiguity.
- `First*` stays correct for key-only queries with no special opt-in.
- Non-safe `First*` is always explicit via `WithNonKeyFilter()`, never implicit.
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
   promises n rows returned, but DynamoDB's `Limit` caps items evaluated. `TranslateTake` becomes
   a translation failure pointing callers to `Limit(n)` as the replacement.
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
6. Restrict `First*` to safe key-only queries by default (PK equality, key predicates only). Any
   `First*` beyond this shape — non-key filters, scan-like paths, or both — requires explicit
   opt-in via `WithNonKeyFilter()`.
7. `WithNonKeyFilter()` is a permission flag recorded in `DynamoQueryCompilationContext`. It removes
   the translation restriction that limits `First*` to safe key-only queries. Execution is
   unchanged: evaluation budget is `n` from `Limit(n)` if present, or DynamoDB's 1MB default
   otherwise. Single request. No paging. When `Limit(n)` is explicitly specified alongside a
   key-only `First*`, `Limit(n)` takes precedence over the provider's implicit `Limit = 1`
   optimisation — the explicit caller instruction wins.
8. `WithNonKeyFilter()` applied to a key-only query with no non-key predicates is a silent no-op.
   `WithNonKeyFilter()` applied to a `ToListAsync()` or other multi-result terminal is also a
   silent no-op — those terminals always accept non-key filters without opt-in.
9. For key-only `First*` without an explicit `Limit(n)`, the provider sets `Limit = 1` on the
   request. Every evaluated item is guaranteed to match, so this is both correct and efficient.
   Without `OrderBy`, the result is the first item in ascending sort key order (DynamoDB's natural
   forward traversal). With `OrderByDescending`, the provider emits ORDER BY and the result is the
   first item in descending sort key order.
10. `Last*` is not supported in this iteration. Translation always fails. Enabling it requires
    implementing reverse traversal (equivalent to `ScanIndexForward = false`) and is deferred to
    follow-on work.
11. Honor explicit ordering when provided, and do not inject ordering when it is not provided.

### Row-limiting shapes reference

| Shape | API | Behaviour |
|---|---|---|
| Any query with evaluation budget | `.Limit(n)` | Evaluate n items, filter, return 0..n. Single request. |
| `First*`, key-only, no explicit budget | `First*` | Provider sets `Limit = 1`. Ascending sort key order unless `OrderBy` overrides. |
| `First*`, key-only, explicit budget | `First*` + `.Limit(n)` | `Limit(n)` wins. Evaluate n items. Single request. |
| `First*`, non-key filter or scan-like | `First*` + `.WithNonKeyFilter()` | Permission flag. Budget from `Limit(n)` or 1MB default. Single request. |
| `First*`, non-key filter or scan-like + explicit budget | `First*` + `.WithNonKeyFilter()` + `.Limit(n)` | Permission flag + evaluation budget. Single request. |
| `First*`, unsafe (no `WithNonKeyFilter()`) | — | Translation failure |
| `Last*` | — | Not supported in this iteration |

### Effective request-limit rule

The provider manages `ExecuteStatementRequest.Limit` internally based on query shape. There is no
user-facing evaluation-limit API.

**`Limit(n)` queries (any shape):**

Exactly one request is sent with `Limit = n`. Returning fewer than `n` results is correct — it
means the filter eliminated some evaluated items, or fewer than `n` items exist in the evaluated
range. There is no paging. `Limit(n)` overrides any implicit `Limit` the provider would otherwise
set (e.g. the `Limit = 1` optimisation for key-only `First*`).

**Key-only `First*` without explicit `Limit(n)`:**

`Limit` is set to `1`. Every item DynamoDB evaluates is guaranteed to match — result count is
0 or 1. This is an internal optimisation; callers cannot observe the difference.

**`First*` with `WithNonKeyFilter()` (any path):**

`Limit` is set from `Limit(n)` if present; otherwise left unset (DynamoDB 1MB default). Single
request. No paging. If no match is found within the evaluated range, null/default is returned.
The caller accepts this when opting in via `WithNonKeyFilter()`. Validation that the flag is
present for non-safe `First*` queries occurs in `DynamoQueryTranslationPostprocessor` after index
selection, where effective key attributes are known — the same stage as ORDER BY validation.
`TranslateFirstOrDefault` records `Limit = 1` as the default request limit and stores any
`WithNonKeyFilter()` flag; the postprocessor then validates and may override the limit.

**`ToListAsync()` (any shape):**

`Limit` is left unset. The provider pages automatically until `NextToken` is absent, collecting
all matching results. Per-request evaluation granularity is managed internally (DynamoDB 1MB
default). Fine-grained per-page control is deferred to Option C.

### Safe path definition

A query shape is **safe** for `First*` without opt-in when the following hold:

1. The WHERE clause contains an equality condition on the partition key of the accessed source
   (base table, GSI, or LSI).
2. The WHERE clause contains **only key predicates** (partition key and/or sort key conditions).
   No non-key attribute filters.

**Special case — no sort key:** When the accessed source has no sort key, each partition contains
at most one item. `First*` on PK equality is always a point lookup and is safe regardless of
whether non-key filters are present. `WithNonKeyFilter()` is silently accepted but not required.

When either condition fails, `First*` requires `WithNonKeyFilter()` to proceed. This covers:

- Non-key filter on a safe path (condition 2 fails).
- Scan-like path (condition 1 fails) — with or without non-key filters. The caller explicitly
  accepts potentially unbounded evaluation cost.

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
- Define the exact translation error messages for `Limit(n)`, `First*`, and `Last*` failure cases.
  `TranslateTake` should fail with a message pointing callers to `Limit(n)`.
- Add diagnostics for unsupported row-limiting shapes.
- Design explicit paging APIs later if needed (Option C), including the per-page evaluation budget
  use case previously served by `WithPageSize`.
- Add the following new API surface:
  - `Limit(n)` extension method on `DynamoDbQueryableExtensions`
  - `WithNonKeyFilter()` extension method on `DynamoDbQueryableExtensions`
  - `NonKeyFilterAllowed` flag on `DynamoQueryCompilationContext` (set by `WithNonKeyFilter()`
    translation, read by postprocessor validation)
  - `Limit` / `LimitExpression` on `SelectExpression` — replaces `PageSize` / `PageSizeExpression`
    for the new `Limit(n)` operator
- Remove the following infrastructure coupled to the old model:
  - `DynamoDbOptionsExtension.DefaultPageSize` and `DynamoDbContextOptionsBuilder.DefaultPageSize()`
  - `DynamoQueryCompilationContext`: `PageSizeOverride`, `PageSizeOverrideExpression`, `SinglePageOnly`
  - `SelectExpression`: `PageSize`, `PageSizeExpression`, `ApplyPageSize()`
  - `SelectExpression`: `ResultLimit`, `ResultLimitExpression`, `ApplyOrCombineResultLimitExpression()`
    — removed with `Take`
  - `QueryingEnumerable`: `_resultLimit` counter and `_pageSize` field
  - `DynamoClientWrapper.ExecutePartiQl`: `singlePageOnly` parameter — tied to `WithoutPagination()`
- Move `WithNonKeyFilter()` validation (safe path check, scan-like check) into
  `DynamoQueryTranslationPostprocessor`, after index selection resolves effective key attributes.
  `TranslateFirstOrDefault` records `Limit = 1` as the default and stores the `WithNonKeyFilter()`
  flag; the postprocessor performs the validation and may override the limit.

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
- **`First*`** is safe by default for key-only queries. `WithNonKeyFilter()` is the universal
  escape hatch for any `First*` that goes beyond the safe key-only shape — non-key filters, scan
  paths, or both. It does not change execution behavior; the caller accepts the evaluation risk.
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
- Strict-by-default `First*` behavior; one clear escape hatch via `WithNonKeyFilter()`.
- Non-safe `First*` is always explicit, never implicit.
- No `WithBestEffortRowLimiting()` or `WithPageSize` to explain — the opt-in surface is small.
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
