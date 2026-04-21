---
icon: lucide/file-stack
---

# Pagination and Evaluation Budget

## Model overview

This provider separates **evaluation budget** (how many items DynamoDB reads) from **terminal behavior**
(what the query returns). There is no general-purpose result limit; instead, use `Limit(n)` to control
the evaluation budget per query.

This page covers three related APIs:

- `Limit(n)` for evaluation budget on a query.
- `ToPageAsync(limit, nextToken)` for one-request page retrieval with continuation output.
- `WithNextToken(token)` for seeding query execution from a saved cursor.

### Evaluation budget: `Limit(n)`

- `.Limit(n)` maps directly to `ExecuteStatementRequest.Limit`.
- DynamoDB evaluates at most `n` items, applies any non-key filters, and returns 0..n results.
- Always a **single request**. No paging.
- `n` must be a positive integer. Zero or negative throws `ArgumentOutOfRangeException`.
- When chained multiple times, the **last call wins**.

### `First*` terminals

- **Safe path** (PK equality + key-only predicates): sets `Limit=1` implicitly. Single request.
- **Unsafe path** (non-key predicate, scan-like, or user `Limit(n)` present): translation fails. Use `.AsAsyncEnumerable()` instead.
- **Derived/shared-table note**: when a discriminator filter is present, `First*` is safe only for
    single-item base-table lookups (PK-only on PK-only table, or PK+SK equality on PK+SK table).

### `ToListAsync()` without `Limit(n)`

- No `Limit` set on request. DynamoDB pages up to 1MB per request.
- Provider follows `NextToken` until exhausted — collects all results.

### `ToListAsync()` with `Limit(n)`

- Single request, `Limit = n`. No paging.

### `ToPageAsync(limit, nextToken)`

- Executes exactly **one** DynamoDB request and returns `DynamoPage<T>`.
- `limit` is DynamoDB evaluation budget (`ExecuteStatementRequest.Limit`), not guaranteed returned item count.
- `nextToken` resumes from a previously saved cursor (`null` means start from beginning).
- Completion is determined **only** by `page.NextToken == null`.
- A page may return fewer than `limit` items (including `0`) and still have a non-null `NextToken`
    when evaluated items were filtered out.

**Terminal/operator rules**

- `ToPageAsync` is terminal and must be used at the query root.
- It is not composable after invocation.
- It cannot be used in subqueries.

**Validation rules**

- `limit` must be positive.
    - Constant non-positive values fail immediately.
    - Parameterized/compiled-query values fail at execution.
- `nextToken` treats empty/whitespace as `null`.

**Conflicts**

- `ToPageAsync` cannot be combined with `.Limit(n)` on the same query shape.

### `WithNextToken(token)`

- Seeds only the **first** request of the query with `token`.
- Subsequent requests (if any) use server continuation tokens as usual.
- `WithNextToken(token).ToListAsync()` resumes full enumeration from the saved cursor.
- `WithNextToken(token).Limit(n).ToListAsync()` performs **one request** from the saved cursor.
- `WithNextToken(token).ToPageAsync(limit, null)` is supported and executes one request from the saved cursor.

**Validation rules**

- `token` must be non-null and non-whitespace.

**Composition/constraints**

- `WithNextToken` can be applied only once per query.
- `WithNextToken` is not supported with key-only `First`/`FirstOrDefault` query shapes.
- When combined with `ToPageAsync(limit, nextToken)`:
    - Allowed if only one source contributes a non-null token.
    - Fails when both `WithNextToken(...)` and `ToPageAsync(..., nextToken: ...)` provide non-null tokens.

## NextToken from response metadata

For queries that use `ToListAsync()` or `AsAsyncEnumerable()` (rather than `ToPageAsync`), the raw
`ExecuteStatementResponse` for each entity's page is available via the `GetExecuteStatementResponse()`
extension on the tracked entity entry:

```csharp
var items = await db.Orders
    .Where(x => x.Status == "Active")
    .Limit(25)
    .ToListAsync(cancellationToken);

// NextToken on this response is the cursor after this page's evaluation budget.
// For Limit(n) queries the provider does not follow it automatically.
var response = db.Entry(items[0]).GetExecuteStatementResponse();
var cursorAfterPage = response?.NextToken;
```

All entities from the same page share the same response object reference. For multi-request
`ToListAsync()` (no `Limit`), continuation tokens have already been consumed by the provider, so
`NextToken` on the response is the cursor to the next pending page, not a saved resume point.
Use `ToPageAsync` when you need explicit cursor control.

## Token semantics

- Tokens are opaque application data from DynamoDB; treat them as pass-through values.
- API termination contract is canonicalized to `null` only:
    - Input normalization: `ToPageAsync(..., nextToken)` maps empty/whitespace to `null`.
    - Response normalization: empty/whitespace response tokens are normalized to `null`.
- End-of-results is always `NextToken == null` (not `Items.Count < limit`).

## Diagnostics

`ExecutingExecuteStatement` includes two token-presence signals:

- `nextTokenPresent`: this request has a token value.
- `seedNextTokenPresent`: the **first request** was seeded by user/query input.

For unseeded queries that continue naturally, continuation requests may have
`nextTokenPresent == true` while `seedNextTokenPresent == false`.

## Unsafe First\* — use AsAsyncEnumerable()

When a `First*` query cannot use the safe key-only path (non-key predicate, scan-like,
discriminator on a multi-item source, or any `Limit(n)` present), translation fails with
`InvalidOperationException`. The correct pattern is to fetch server-side results then select
client-side:

```csharp
// Unsafe shape — non-key filter: use AsAsyncEnumerable()
var active = await db.Orders
    .Where(x => x.UserId == userId && x.IsActive)
    .AsAsyncEnumerable()
    .FirstOrDefaultAsync(cancellationToken);
```

Add `.Limit(n)` before `AsAsyncEnumerable()` only when you intentionally want a bounded sample.

This is the standard EF Core pattern for explicit client-side evaluation: `AsAsyncEnumerable()`
marks where server-side evaluation ends and LINQ-to-objects begins.

## No global default

There is no `DefaultPageSize` option. Use `.Limit(n)` per query.

## Evaluation budget reference

| Shape                                             | `ExecuteStatementRequest.Limit` | Pages?      |
| ------------------------------------------------- | ------------------------------- | ----------- |
| `.Limit(n)` + `ToListAsync()`                     | `n`                             | No          |
| `.ToPageAsync(n, token)`                          | `n`                             | One request |
| `.WithNextToken(token)` + `ToListAsync()`         | `null` (1MB per page)           | Yes         |
| `.WithNextToken(token)` + `.Limit(n)`             | `n`                             | No          |
| `.WithNextToken(token)` + `.ToPageAsync(n, null)` | `n`                             | One request |
| `First*` (key-only, no explicit limit)            | `1`                             | No          |
| `First*` + any `Limit(n)`                         | **Translation failure**         | —           |
| `First*` on non-key/scan-like path                | **Translation failure**         | —           |
| `.Limit(n)` + `AsAsyncEnumerable()` + `First*`    | `n` (client-side selection)     | No          |
| `ToListAsync()` (no limit)                        | `null` (1MB per page)           | Yes         |

## Examples

### Evaluation budget on a scan

```csharp
// Evaluate 25 items, apply IsActive filter, return 0..25.
var orders = await db.Orders
    .Where(x => x.IsActive)
    .Limit(25)
    .ToListAsync(cancellationToken);
```

### First\* with non-key filter — use AsAsyncEnumerable()

```csharp
// Evaluate 50 items server-side, then take first match client-side.
var active = await db.Orders
    .Where(x => x.UserId == userId && x.IsActive)
    .Limit(50)
    .AsAsyncEnumerable()
    .FirstOrDefaultAsync(cancellationToken);
```

### Chaining `Limit(n)` — last call wins

```csharp
// Effective limit is 20.
await db.Orders.Limit(10).Limit(20).ToListAsync(cancellationToken);
```

### Filtered page can return zero items and still continue

```csharp
var page = await db.Orders
    .Where(x => x.IsActive)
    .ToPageAsync(25, nextToken, cancellationToken);

// Valid: page.Items.Count == 0 while page.NextToken is non-null.
// Continue until NextToken is null.
```

### Resume all remaining results from a saved token

```csharp
var remaining = await db.Orders
    .Where(x => x.Status == "Active")
    .WithNextToken(savedToken)
    .ToListAsync(cancellationToken);
```

### Resume one request from a saved token

```csharp
var chunk = await db.Orders
    .Where(x => x.Status == "Active")
    .WithNextToken(savedToken)
    .Limit(25)
    .ToListAsync(cancellationToken);
```

### One-request page from saved token

```csharp
var page = await db.Orders
    .Where(x => x.Status == "Active")
    .WithNextToken(savedToken)
    .ToPageAsync(25, null, cancellationToken);
```

### Ambiguous token source (invalid)

```csharp
// Invalid: both sources contribute a non-null token.
await db.Orders
    .WithNextToken(savedToken)
    .ToPageAsync(25, otherToken, cancellationToken);
```

### Compiled query with runtime parameter

```csharp
var query = EF.CompileAsyncQuery((OrderDbContext ctx, int n)
    => ctx.Orders.Limit(n));

var results = await query(db, 50).ToListAsync(cancellationToken);
```

## External references

- AWS ExecuteStatement API: <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_ExecuteStatement.html>
- DynamoDB PartiQL SELECT: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.select.html>
