# ADR-002: Pagination model options for DynamoDB queries

## Status

- Proposed
- **Date:** 2026-03-20
- **Deciders:** EntityFrameworkCore.DynamoDb maintainers
- **Supersedes:** none

---

## Context

The current provider supports row-limiting operators such as `Take`, `FirstAsync`, and
`FirstOrDefaultAsync`, along with provider-specific query extensions such as `WithPageSize(...)`,
`DefaultPageSize(...)`, and `WithoutPagination()`.

Today these concepts are intentionally separate:

- **Result limit:** how many rows EF should return.
- **Page size:** how many items DynamoDB should evaluate per `ExecuteStatement` request.
- **Pagination continuation:** whether the provider follows DynamoDB `NextToken` across requests.

That split exists because DynamoDB `ExecuteStatementRequest.Limit` is not SQL `LIMIT`. It caps the
number of items DynamoDB evaluates in that request. Matching/filtering happens after evaluation, and
responses can return zero matching rows while still producing `NextToken`.

The current provider behavior is therefore:

- `Take(n)` sets a provider-side result limit.
- `First*` sets an effective result limit of `1`.
- `WithPageSize(n)` maps to DynamoDB request `Limit`.
- `DefaultPageSize(n)` is a fallback when no per-query page size is set.
- `WithoutPagination()` stops after the first DynamoDB response even if more results may exist.
- The query pipeline enforces result limits while continuing to read DynamoDB pages until enough
  matching rows have been materialized or `NextToken` is exhausted.

Current API examples:

```csharp
var items = await db.SimpleItems
    .WithPageSize(25)
    .Where(x => x.Pk == "ITEM#1" && x.BoolValue)
    .Take(3)
    .ToListAsync();
```

```csharp
var first = await db.SimpleItems
    .Where(x => x.Pk == "ITEM#1" && x.BoolValue)
    .FirstOrDefaultAsync(pageSize: 25, cancellationToken);
```

Operationally, this means a query can evaluate more items than it ultimately returns. That is
acceptable for one-shot row-limited queries, but it becomes problematic for explicit user-facing
paging.

Example problem:

```csharp
var page = await db.SimpleItems
    .WithPageSize(50)
    .Where(x => x.Category == "A" && x.IsActive)
    .Take(3)
    .ToListAsync();
```

If DynamoDB evaluates 50 items and only 3 match, returning those 3 is fine for a one-shot query.
But if the provider were to expose a continuation token for this logical page and simply forward the
underlying DynamoDB `NextToken`, it would resume after the 50 evaluated items. Any matching rows in
the remainder of that response window would be lost to the next logical page.

This ADR exists because the provider is not yet published, so breaking changes are still acceptable.
It is the right time to decide whether the public API should keep the current model, rename it to be
more explicit, or introduce separate APIs for different pagination semantics.

## Decision Drivers

- Preserve correctness for user-visible pagination; no silently lost rows.
- Keep one-shot query semantics understandable for EF Core users.
- Respect DynamoDB `ExecuteStatement` semantics instead of pretending they are SQL semantics.
- Make performance trade-offs explicit rather than implicit.
- Avoid ambiguous terminology where one API tries to mean both result count and evaluation budget.
- Leave room for Dynamo-native and access-pattern-optimized APIs later.
- Learn from other providers without copying abstractions that do not fit DynamoDB.

## Current State

### Current semantics

- `Take(n)` and `First*` are **result-limiting** operators.
- `WithPageSize(n)` and `DefaultPageSize(n)` are **evaluation-budget** controls.
- `WithoutPagination()` opts out of following `NextToken` and knowingly allows incomplete results.
- The provider does not emit PartiQL `LIMIT` for these operators; result limiting is enforced during
  enumeration.

### Current strengths

- Accurately models DynamoDB's `Limit` behavior.
- Gives advanced users a way to tune request size separately from result count.
- Works well enough for one-shot operations such as `FirstAsync(pageSize: ...)`.

### Current weaknesses

- The name `WithPageSize` sounds like logical page size, but it is actually a request evaluation
  budget.
- The current model becomes dangerous if reused for explicit continuation-based paging.
- `WithoutPagination()` intentionally breaks EF-style completeness guarantees.
- EF users will naturally compare this to SQL-style `Take` and to other providers which do not
  expose this split on normal LINQ queries.

### Comparison to other providers

#### Cosmos provider

Cosmos mostly expresses row limiting in the translated query itself:

- `Take(n)` becomes `OFFSET 0 LIMIT n`.
- `First*` becomes `LIMIT 1`.
- `Single*` becomes `LIMIT 2`.

Cosmos still pages internally via feed iterators, but normal LINQ queries hide continuation tokens.
When Cosmos exposes explicit paging, it uses a separate `ToPageAsync(pageSize, continuationToken)`
API rather than overloading normal `Take` semantics.

Cosmos-style example:

```csharp
var page = await context.Customers
    .OrderBy(x => x.Id)
    .ToPageAsync(pageSize: 10, continuationToken: token);
```

#### Mongo provider

Mongo mostly delegates `Take`, `Skip`, `First*`, and `Single*` semantics to the Mongo LINQ/driver
pipeline:

- `Take(n)` becomes `$limit: n`.
- `Skip(n)` becomes `$skip: n`.
- `First*` becomes `$limit: 1`.
- `Single*` becomes `$limit: 2`.

The provider does not appear to expose provider-level continuation tokens or a public page-size API
for normal query paging.

#### Implication for this provider

This provider is the outlier because DynamoDB forces a distinction between:

- how many matching rows the caller wants, and
- how many items DynamoDB evaluates per request.

That distinction is valid, but the public API must name and scope it carefully.

## Options Considered

### Option A: Keep the current model and clarify naming only

Retain the current behavior, but rename `WithPageSize` and `DefaultPageSize` to something closer to
their real meaning, such as `WithEvaluationLimit` and `DefaultEvaluationLimit`.

Example API:

```csharp
var items = await db.SimpleItems
    .WithEvaluationLimit(25)
    .Where(x => x.Pk == "ITEM#1" && x.BoolValue)
    .Take(3)
    .ToListAsync();
```

**Pros:**

- Smallest change to implementation and mental model.
- Makes the current behavior more honest and less surprising.
- Keeps the useful separation between result limit and request evaluation budget.

**Cons:**

- Does not solve explicit logical paging by itself.
- Still leaves advanced Dynamo-specific tuning directly on ordinary LINQ queries.
- EF users may still expect a first-class logical paging API elsewhere.

### Option B: Add a correctness-first logical page API

Introduce a separate API where `pageSize` means returned results, not evaluated items. For this API,
the provider keeps requesting more data until it has collected `pageSize` matching rows or there are
no more rows.

The simplest correct form of this API couples Dynamo request `Limit` to the remaining requested
results so the provider never overreads past a logical page boundary.

Example API:

```csharp
var page = await db.SimpleItems
    .Where(x => x.Category == "A" && x.IsActive)
    .OrderBy(x => x.SortKey)
    .ToPageAsync(pageSize: 10, continuationToken: token, cancellationToken);
```

Possible result contract:

```csharp
public sealed record DynamoPage<T>(
    IReadOnlyList<T> Values,
    string? ContinuationToken,
    bool HasMoreResults);
```

**Pros:**

- Gives users the paging contract they usually expect.
- Avoids silent data loss between logical pages.
- Fits application-facing paging scenarios well.

**Cons:**

- Can require many DynamoDB requests for selective filters.
- May have higher latency than a Dynamo-native page API.
- Needs a clear continuation-token contract.

### Option C: Expose a Dynamo-native service page API

Expose paging that matches DynamoDB semantics directly. A page means one service page or one
evaluation window, not a guaranteed number of matching rows.

Example API:

```csharp
var page = await db.SimpleItems
    .Where(x => x.Category == "A" && x.IsActive)
    .ToDynamoPageAsync(evaluationLimit: 50, continuationToken: token, cancellationToken);
```

Possible result contract:

```csharp
public sealed record DynamoServicePage<T>(
    IReadOnlyList<T> Values,
    string? ContinuationToken,
    int EvaluatedItemCount,
    bool IsTruncated);
```

**Pros:**

- Honest to DynamoDB semantics.
- Efficient and easy to reason about operationally.
- Avoids pretending service pages are logical pages.

**Cons:**

- Returned item count can vary wildly.
- Empty pages are possible even when more matching results exist later.
- Less familiar to EF users and application developers.

### Option D: Offer both logical paging and Dynamo-native paging

Provide two separate APIs with explicit names and semantics.

Example API:

```csharp
var logicalPage = await db.SimpleItems
    .Where(x => x.Category == "A" && x.IsActive)
    .OrderBy(x => x.SortKey)
    .ToPageAsync(pageSize: 10, continuationToken: token, cancellationToken);

var servicePage = await db.SimpleItems
    .Where(x => x.Category == "A" && x.IsActive)
    .ToDynamoPageAsync(evaluationLimit: 50, continuationToken: token, cancellationToken);
```

**Pros:**

- Gives users explicit choice between correctness-first and Dynamo-native behavior.
- Reduces ambiguity in naming and expectations.
- Aligns well with the fact that DynamoDB supports two genuinely different notions of paging.

**Cons:**

- Larger API surface.
- Requires strong docs and diagnostics so users choose the right API.
- More testing and maintenance work.

### Option E: Logical paging with opaque provider tokens that include buffered leftovers

Keep a user-friendly exact-page API while allowing the provider to overread internally. Instead of
returning raw DynamoDB `NextToken`, return an opaque provider token that contains:

- the underlying DynamoDB `NextToken`,
- any unread buffered rows from the last evaluation window,
- versioning/query-shape metadata.

Example API:

```csharp
var page1 = await db.SimpleItems
    .Where(x => x.Category == "A" && x.IsActive)
    .ToPageAsync(pageSize: 10, continuationToken: null, cancellationToken);

var page2 = await db.SimpleItems
    .Where(x => x.Category == "A" && x.IsActive)
    .ToPageAsync(pageSize: 10, continuationToken: page1.ContinuationToken, cancellationToken);
```

**Pros:**

- Best user-facing experience for exact pages.
- Allows internal prefetching or larger evaluation windows.
- Avoids lost rows if implemented correctly.

**Cons:**

- Highest implementation complexity.
- Token size can grow.
- Requires token versioning, validation, and likely signing/encryption.
- Debugging and support become harder.

### Option F: Stateful server-side cursors

Store unread buffered state on the server or in an external store and hand the caller a cursor id.

Example API:

```csharp
var page = await db.SimpleItems
    .Where(x => x.Category == "A" && x.IsActive)
    .ToPageAsync(pageSize: 10, cursor: cursorId, cancellationToken);
```

**Pros:**

- Small client token.
- Supports exact pages without packing state into the token.

**Cons:**

- Introduces statefulness, expiration, cleanup, and storage concerns.
- Poor fit for a reusable EF Core provider library.
- Harder to host and operate.

### Option G: Keyset pagination for supported ordered query shapes

Prefer seek/keyset pagination when the query shape aligns with DynamoDB access patterns, typically a
partition-constrained query ordered by sort key.

Example API:

```csharp
var page1 = await db.Orders
    .Where(x => x.Pk == tenantId)
    .OrderBy(x => x.Sk)
    .ToKeysetPageAsync(pageSize: 20, after: null, cancellationToken);

var page2 = await db.Orders
    .Where(x => x.Pk == tenantId)
    .OrderBy(x => x.Sk)
    .ToKeysetPageAsync(pageSize: 20, after: page1.LastKey, cancellationToken);
```

**Pros:**

- Fits DynamoDB's natural access patterns well.
- Avoids the leftover-items problem for supported shapes.
- Produces compact tokens or last-key markers.
- Can be the most efficient and understandable model for key-ordered access.

**Cons:**

- Does not support arbitrary query shapes.
- Requires explicit ordering/access-pattern validation.
- Probably complements rather than replaces another paging model.

## Decision

We should move toward **Option D: offer both logical paging and Dynamo-native paging**, while also
adopting the naming cleanup from **Option A**.

Specifically:

1. Rename current `WithPageSize` and `DefaultPageSize` APIs to evaluation-oriented names.
2. Treat those renamed APIs as request-tuning controls for ordinary one-shot queries.
3. Introduce a separate logical paging API whose contract is returned-results-first and does not
   silently lose rows.
4. Optionally introduce a separate Dynamo-native service page API for users who want backend-truthful
   semantics.
5. Explore keyset pagination later as the preferred model for supported ordered key-based queries.

## Rationale

No single pagination abstraction cleanly covers both application-friendly paging and DynamoDB-native
evaluation windows.

Trying to make one API do both leads directly to the current ambiguity:

- EF users hear "page size" and think "number of returned rows".
- DynamoDB uses `Limit` to mean "number of evaluated items in this request".

Those are not the same thing, and hiding the difference creates correctness problems once
continuation tokens are involved.

Option D gives the provider an honest split:

- ordinary query tuning remains available for advanced users,
- logical paging has a correctness-first contract,
- Dynamo-native paging remains possible without pretending it is EF-style paging.

This also matches prior art from other providers without copying them blindly:

- Cosmos separates normal LINQ row limiting from explicit continuation paging.
- Mongo mostly leaves paging and batching to backend query semantics and driver behavior.

For DynamoDB, explicit semantic separation is even more important because backend evaluation limits
and returned result counts diverge by design.

## Consequences

**Positive:**

- Public API becomes more honest about DynamoDB behavior.
- A future logical paging API can be correct by construction.
- Advanced users keep access to request-level tuning.
- Docs and diagnostics can clearly explain when users are choosing efficiency over completeness.

**Negative / Trade-offs:**

- Breaking rename for existing pagination-tuning APIs.
- More than one paging concept to document.
- Logical paging may be slower on selective queries.
- If opaque continuation tokens are chosen later, implementation complexity will increase.

**Neutral / Follow-on work:**

- Decide final names for evaluation-budget APIs.
- Decide whether the first logical paging implementation should use strict remaining-result request
  limits or opaque provider tokens.
- Decide whether Dynamo-native paging should ship in the first version or later.
- Define token format/versioning rules before exposing any public continuation API.
- Add diagnostics explaining why selective filters can require many requests for logical pages.
- Evaluate whether `WithoutPagination()` should remain, be renamed, or be restricted to service-page
  scenarios.
- Add documentation and examples that explicitly show the difference between result limits,
  evaluation limits, and continuation semantics.
- Investigate keyset paging APIs for partition-key + sort-key ordered queries.

## Suggested Migration Shape

### Ordinary one-shot query tuning

```csharp
var first = await db.SimpleItems
    .Where(x => x.Pk == "ITEM#1" && x.BoolValue)
    .WithEvaluationLimit(25)
    .FirstOrDefaultAsync(cancellationToken);
```

### Correct logical paging

```csharp
var page = await db.SimpleItems
    .Where(x => x.Pk == "ITEM#1" && x.BoolValue)
    .OrderBy(x => x.Sk)
    .ToPageAsync(pageSize: 10, continuationToken: token, cancellationToken);
```

### Explicit Dynamo-native paging

```csharp
var page = await db.SimpleItems
    .Where(x => x.Pk == "ITEM#1" && x.BoolValue)
    .ToDynamoPageAsync(evaluationLimit: 25, continuationToken: token, cancellationToken);
```

### Future keyset paging for supported shapes

```csharp
var page = await db.SimpleItems
    .Where(x => x.Pk == "ITEM#1")
    .OrderBy(x => x.Sk)
    .ToKeysetPageAsync(pageSize: 10, after: lastSeenSortKey, cancellationToken);
```

## References

- AWS ExecuteStatement API:
  <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_ExecuteStatement.html>
- DynamoDB PartiQL SELECT:
  <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.select.html>
- DynamoDB Query filter expressions:
  <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/Query.FilterExpression.html>
- Repository docs: `docs/pagination.md`, `docs/operators.md`, `docs/limitations.md`,
  `docs/configuration.md`
- Repository code:
  `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoDbQueryableExtensions.cs`,
  `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`,
  `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.cs`,
  `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.QueryingEnumerable.cs`,
  `src/EntityFrameworkCore.DynamoDb/Storage/DynamoClientWrapper.cs`
- EF Core Cosmos prior art:
  `/Users/jonasha/Repos/CSharp/efcore/src/EFCore.Cosmos/Extensions/CosmosQueryableExtensions.cs`,
  `/Users/jonasha/Repos/CSharp/efcore/src/EFCore.Cosmos/Query/Internal/CosmosQueryableMethodTranslatingExpressionVisitor.cs`
- Mongo EF Core prior art:
  `/Users/jonasha/Repos/CSharp/mongo-efcore-provider/src/MongoDB.EntityFrameworkCore/Query/Visitors/MongoQueryableMethodTranslatingExpressionVisitor.cs`,
  `/Users/jonasha/Repos/CSharp/mongo-efcore-provider/src/MongoDB.EntityFrameworkCore/Query/Visitors/MongoEFToLinqTranslatingExpressionVisitor.cs`
