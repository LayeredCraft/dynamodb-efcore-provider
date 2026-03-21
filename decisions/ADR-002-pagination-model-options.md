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

Keep an evaluation-limit concept, but stop pretending it is row-limit semantics.

Under this model:

- `WithEvaluationLimit(...)` is request tuning only.
- `Take(n)` means "return the first `n` items in the query's actual order".
- `First*` means `Take(1)`.
- `Last*` means the last item in the query's order and is only supported when that order is actually
  well-defined.
- If the user specifies `OrderBy(...)` / `OrderByDescending(...)`, the provider honors it.
- If the user does not specify ordering, the provider leaves ordering alone and does not inject one.

By default, unsafe row-limiting shapes fail translation.

Add an explicit escape hatch for users who want Dynamo-style best-effort behavior:

- global option: `AllowBestEffortRowLimiting`
- per-query option: `.AllowBestEffortRowLimiting()`

That opt-in means the caller accepts that non-key filters may require reading forward across requests
to find enough matches.

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

## Decision

We will adopt **Option B** as the core model, and keep **Option C** for later.

### What we are doing now

1. Rename `WithPageSize` and `DefaultPageSize` to evaluation-oriented names such as
   `WithEvaluationLimit` and `DefaultEvaluationLimit`.
2. Treat those APIs strictly as request-tuning controls.
3. Keep LINQ strict by default: if a row-limiting shape is not safe, fail translation.
4. Add explicit opt-in for unsafe shapes via `AllowBestEffortRowLimiting`.
5. Honor explicit ordering when provided, and do not inject ordering when it is not provided.
6. Support `First*` and `Take` without explicit `OrderBy`; they operate on the backend-returned order
   of the query.
7. Support `Last*` only when the order is well-defined; otherwise require best-effort opt-in or
   reject translation.

### Effective request-limit rule

When a query has both row limiting and request tuning, row limiting wins.

- `WithEvaluationLimit(25).Take(10)` -> effective request limit is `10`
- `WithEvaluationLimit(5).Take(10)` -> effective request limit is `5`, then continue if needed
- `First*` behaves like `Take(1)`

In other words, the request limit for each call is:

- `min(configured evaluation limit, remaining results needed)`

That keeps `Take` / `First*` as the semantic operator and `WithEvaluationLimit(...)` as a tuning
hint.

### What we are not doing now

- We are not using request evaluation limits as a substitute for LINQ row semantics.
- We are not injecting a default ordering when the user did not request one.
- We are not exposing continuation paging through normal LINQ operators.

### Follow-on work

- Re-evaluate `WithoutPagination()` under this model.
- Define the exact safe-query rules for `Take`, `First*`, and `Last*`.
- Add diagnostics for unsupported row-limiting shapes.
- Design explicit paging APIs later if needed.

## Rationale

This keeps the model clear:

- **LINQ operators** describe result semantics.
- **Evaluation limit** describes request behavior.
- **Best-effort row limiting** is explicit, not hidden.
- **Paging** stays separate and can be designed like Cosmos `ToPageAsync(...)` later.

This also fits what we learned from other providers:

- Cosmos keeps row limiting separate from explicit paging.
- Mongo relies on backend query semantics and does not expose provider paging for normal LINQ.

For DynamoDB, keeping these concepts separate matters even more because evaluated item count and
returned row count can diverge by design.

## Consequences

**Positive:**

- Easier-to-explain semantics.
- Strict-by-default LINQ behavior.
- Explicit escape hatch for Dynamo-style behavior.
- Clean path to future paging APIs.

**Trade-offs:**

- Some currently supported shapes become opt-in or translation failures.
- Users need to understand the difference between row limiting and evaluation limit.
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
