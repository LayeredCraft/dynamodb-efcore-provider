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

This creates two problems:

- normal LINQ operators can drift into provider-managed trimming instead of strict EF semantics
- any future paging API can lose rows if it forwards DynamoDB `NextToken` after over-evaluating a
  page

Additionally, exposing an evaluation-limit API at all keeps the conceptual confusion alive even with
better naming. The `WithPageSize` / `DefaultPageSize` API was the root cause of this confusion and
should be removed, not renamed.

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
- Does not solve the paging model.

### Option B: Strict by default, explicit best-effort opt-in

Remove the evaluation-limit API entirely. The provider manages request sizing internally based on
the query shape. LINQ operators are the only row-limiting surface.

Under this model:

- `Take(n)` means "return the first `n` items in the query's effective backend order".
- `First*` means `Take(1)`.
- `Last*` means the last item in the query's order and is only supported when that order is
  well-defined.
- If the user specifies `OrderBy(...)` / `OrderByDescending(...)`, the provider honors it.
- If the user does not specify ordering, the provider leaves ordering alone and does not inject one.
- Natural ordering is considered reliable only for true DynamoDB queryable access paths that have a
  sort key (table/index key order; reverse direction maps to low-level forward/reverse traversal).
- Scan-like shapes and access paths without a sort key are not treated as having a strict ordering
  guarantee.

By default, unsafe row-limiting shapes fail translation.

Add an explicit escape hatch for users who want Dynamo-style best-effort behavior:

- global option: `AllowBestEffortRowLimiting`
- per-query option: `.AllowBestEffortRowLimiting()`

That opt-in means the caller accepts that non-key filters may require reading forward across requests
to find enough matches. When enabled, the provider continues paging DynamoDB requests until the
requested row count is satisfied or results are exhausted — it does not stop at the first page.

Example of a safe query:

```csharp
var latest = await db.Orders
    .Where(x => x.TenantId == tenantId)
    .OrderByDescending(x => x.CreatedAt)
    .FirstOrDefaultAsync(cancellationToken);
```

Example of an unsafe query that would require explicit opt-in:

```csharp
var items = await db.Orders
    .Where(x => x.TenantId == tenantId && x.IsActive)
    .AllowBestEffortRowLimiting()
    .Take(10)
    .ToListAsync(cancellationToken);
```

**Pros:**

- Keeps normal LINQ semantics strict by default.
- Makes the Dynamo-specific trade-off explicit.
- Still gives users a practical escape hatch when they want it.

**Cons:**

- Some current query shapes become translation failures by default.
- Requires clear diagnostics and docs.

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

Keyset paging is also a strong future option for ordered key queries.

If this API is added, continuation must use an opaque provider cursor contract rather than exposing
raw backend tokens.

## Decision

We will adopt **Option B** as the core model, and keep **Option C** for later.

### What we are doing now

1. Remove `WithPageSize`, `DefaultPageSize`, and the `FirstAsync(pageSize, ...)` /
   `FirstOrDefaultAsync(pageSize, ...)` convenience overloads entirely.
2. Keep LINQ strict by default: if a row-limiting shape is not safe, fail translation.
3. Add explicit opt-in for unsafe shapes via `AllowBestEffortRowLimiting`.
4. Honor explicit ordering when provided, and do not inject ordering when it is not provided.
5. Support `First*` and `Take` without explicit `OrderBy`; they operate on the backend-returned order
   only when the selected access path has a reliable natural order (query + sort key).
6. Support `Last*` only when the order is well-defined; otherwise require best-effort opt-in or
   reject translation.

### Effective request-limit rule

The provider manages `ExecuteStatementRequest.Limit` internally based on query shape. There is no
user-facing evaluation-limit API.

**Safe paths** (partition key equality + sort key present):

- `Limit` is set to the number of remaining results needed per request.
- `Take(10)` sends `Limit = 10` on the first request, `Limit = remaining` on each continuation.
- `First*` behaves like `Take(1)`.
- This is efficient: key-ordered traversal finds results immediately, so small limits are correct.

**Unsafe paths** (scan-like, `AllowBestEffortRowLimiting` active):

- `Limit` is left unset (DynamoDB default of 1MB per request).
- Non-key filters may be sparse; capping `Limit` to remaining results would produce many tiny,
  inefficient requests. DynamoDB's natural page size is the right default here.
- Users who need fine-grained evaluation-limit control on unsafe paths should use the future
  explicit paging API (Option C) or the AWS SDK directly.

### Safe path definition

A query shape is **safe** for strict row limiting when both of the following hold:

1. The WHERE clause contains an equality condition on the partition key of the accessed source
   (base table, GSI, or LSI).
2. The accessed source has a sort key.

A shape is **unsafe** when either condition fails: no equality on the partition key (scan shape),
or the source has no sort key.

For GSIs: the GSI's own partition key must be equality-filtered and the GSI must define a sort key.
An LSI inherits the table partition key, so condition 1 is satisfied when the table partition key
is equality-filtered.

This definition is the gate for the strict-by-default rule. Any `Take`, `First*`, or `Last*` on an
unsafe shape fails translation unless `AllowBestEffortRowLimiting` is active.

### What we are not doing now

- We are not exposing an evaluation-limit tuning API on the LINQ surface; if needed it belongs in
  the future explicit paging API (Option C).
- We are not injecting a default ordering when the user did not request one.
- We are not promising scan order or partition-key-only global order as a strict contract.
- We are not exposing continuation paging through normal LINQ operators.

### Follow-on work

- `WithoutPagination()` is deprecated under this model. It will be removed when explicit paging APIs
  (Option C) are introduced. Until then it remains callable but is documented as: equivalent to
  `AllowBestEffortRowLimiting()` with a single-page cap. It does not become part of the
  `AllowBestEffortRowLimiting` opt-in path — it is a temporary survival shim only.
- Define the exact safe-query rules for `Take`, `First*`, and `Last*`.
- Add diagnostics for unsupported row-limiting shapes.
- Design explicit paging APIs later if needed.

### Continuation state and cursor contract

- Strict LINQ paths (`Take`, `First*`, `Last*`) keep continuation internal to query execution and do
  not expose backend pagination state.
- If/when explicit paging APIs are introduced, the provider returns an opaque cursor token.
- The opaque cursor encapsulates backend continuation state (currently `NextToken` for PartiQL
  `ExecuteStatement`; potentially `LastEvaluatedKey` / `ExclusiveStartKey` if low-level query paths
  are introduced later).
- Raw backend continuation values are not the public API contract.
- Cursor design must prevent skipped results across calls when best-effort row limiting is enabled.

## Rationale

This keeps the model clear:

- **LINQ operators** describe result semantics.
- **Request sizing** is managed internally by the provider, not exposed to callers.
- **Best-effort row limiting** is explicit, not hidden.
- **Paging** stays separate and can be designed like Cosmos `ToPageAsync(...)` later.
- **Continuation state** is wrapped behind an opaque provider cursor for explicit paging APIs.

This also fits what we learned from other providers:

- Cosmos keeps row limiting separate from explicit paging.
- Mongo relies on backend query semantics and does not expose provider paging for normal LINQ.

For DynamoDB, keeping these concepts separate matters even more because evaluated item count and
returned row count can diverge by design.

For ordering specifically, strict assumptions are limited to queryable access paths with sort keys:

- low-level DynamoDB query traversal is key-ordered,
- direction is controlled by forward/reverse traversal,
- scan-like paths do not provide a strict ordering guarantee for LINQ semantics.

## Consequences

**Positive:**

- Easier-to-explain semantics.
- Strict-by-default LINQ behavior.
- Explicit escape hatch for Dynamo-style behavior.
- Clean path to future paging APIs.

**Trade-offs:**

- Some currently supported shapes become opt-in or translation failures.
- `WithPageSize` / `DefaultPageSize` and their convenience overloads are removed with no direct
  replacement on the LINQ surface; users who need per-request evaluation tuning must wait for the
  explicit paging API or use the AWS SDK directly.
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
