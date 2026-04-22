---
title: Pagination
description: How pagination works using continuation tokens, the ToPageAsync pattern, and WithNextToken.
---

# Pagination

_The provider exposes DynamoDB's cursor-based continuation model through `ToPageAsync` and `WithNextToken`; there are no integer offsets — pagination is always driven by opaque tokens returned by DynamoDB._

## Continuation Tokens

DynamoDB returns an opaque `NextToken` string when a response was cut short by the evaluation budget (`Limit`) or the 1 MB per-request data cap. A non-null `NextToken` means more results may exist; `NextToken == null` means the result set is exhausted.

Tokens are pass-through values — the provider does not parse or manipulate them. Empty or whitespace token strings are normalized to `null`. You can store a token and use it later to resume the query from that position.

!!! note

    End of results is always `NextToken == null`. Do not use `Items.Count < limit` as a termination condition — a page can legitimately return zero items while still having a non-null `NextToken` when all evaluated items were filtered out by a non-key `Where` predicate.

## ToPageAsync

`ToPageAsync(limit, nextToken, cancellationToken)` executes exactly one DynamoDB request and returns a `DynamoPage<T>`. The page contains:

- `Items` — the matching results from this request.
- `NextToken` — the continuation cursor for the next page, or `null` at end of results.
- `HasMoreResults` — shorthand for `NextToken != null`.

`limit` is the evaluation budget for this request, not a guarantee of how many items are returned. A page may return fewer than `limit` items (including zero) and still have a non-null `NextToken` if filtered items exhausted the budget.

```csharp
// Canonical pagination loop
string? token = null;

do
{
    var page = await db.Orders
        .Where(o => o.CustomerId == customerId)
        .ToPageAsync(25, token, cancellationToken);

    foreach (var order in page.Items)
    {
        // process order
    }

    token = page.NextToken;
}
while (token != null);
```

`ToPageAsync` cannot be combined with `.Limit(n)` on the same query — the limit is supplied as the first argument to `ToPageAsync` itself.

## WithNextToken

`.WithNextToken(token)` seeds the first request of the query at a previously saved cursor. Subsequent requests (when the provider follows continuation automatically) use server-returned tokens as usual.

```csharp
// Resume full enumeration from a saved token
var remaining = await db.Orders
    .Where(o => o.Status == "PENDING")
    .WithNextToken(savedToken)
    .ToListAsync(cancellationToken);

// Fetch one bounded chunk from a saved token
var chunk = await db.Orders
    .Where(o => o.Status == "PENDING")
    .WithNextToken(savedToken)
    .Limit(25)
    .ToListAsync(cancellationToken);

// Single paged request from a saved token
var page = await db.Orders
    .Where(o => o.Status == "PENDING")
    .WithNextToken(savedToken)
    .ToPageAsync(25, null, cancellationToken);
```

`WithNextToken` accepts only a non-null, non-whitespace string. It can be applied once per query. When combined with `ToPageAsync(limit, nextToken)`, only one of the two token sources may be non-null; providing a non-null token to both throws at execution.

`WithNextToken` is not supported with key-only `First`/`FirstOrDefault` query shapes.

## Page Size vs Evaluation Budget

The evaluation budget and the number of returned items are separate concerns:

- The budget (`limit` in `ToPageAsync`, or `n` in `Limit(n)`) controls how many items DynamoDB reads and evaluates filter expressions against.
- The number of items in `page.Items` is how many of those evaluated items matched the `WHERE` predicate.

With a highly selective filter, DynamoDB may evaluate the entire budget without returning any matching items — yet still set `NextToken` because items beyond the budget position were not checked. Keep paginating until `NextToken == null`.

!!! note

    There is no global default page size. Specify the evaluation budget explicitly on each `ToPageAsync` call or via `Limit(n)`.

## Evaluation Budget Reference

| Query Shape                                             | `ExecuteStatementRequest.Limit` | Pages?              |
| ------------------------------------------------------- | ------------------------------- | ------------------- |
| `.Limit(n)` + `ToListAsync()`                           | `n`                             | No (single request) |
| `.ToPageAsync(n, token)`                                | `n`                             | One request         |
| `.WithNextToken(token)` + `ToListAsync()`               | `null` (1 MB/page)              | Yes                 |
| `.WithNextToken(token)` + `.Limit(n)` + `ToListAsync()` | `n`                             | No (single request) |
| `.WithNextToken(token)` + `.ToPageAsync(n, null)`       | `n`                             | One request         |
| `First*` (key-only, no explicit limit)                  | `1`                             | No                  |
| `First*` + any `Limit(n)`                               | Translation failure             | —                   |
| `.Limit(n)` + `AsAsyncEnumerable()` + `First*`          | `n` (client-side selection)     | No                  |
| `ToListAsync()` (no limit)                              | `null` (1 MB/page)              | Yes                 |

## Accessing the Raw Response Token

For `Limit(n) + ToListAsync()` queries, the provider does not follow `NextToken` automatically — the query is a single request. If you want to continue from where the evaluation budget stopped, retrieve the response token from the tracked entity entry and use it to seed the next query:

```csharp
var items = await db.Orders
    .Where(o => o.Status == "ACTIVE")
    .Limit(25)
    .ToListAsync(cancellationToken);

// Cursor at the end of this evaluation range
var response = db.Entry(items[0]).GetExecuteStatementResponse();
var nextCursor = response?.NextToken;

// Resume with WithNextToken on a subsequent query
if (nextCursor != null)
{
    var nextChunk = await db.Orders
        .Where(o => o.Status == "ACTIVE")
        .WithNextToken(nextCursor)
        .Limit(25)
        .ToListAsync(cancellationToken);
}
```

All items from the same request share the same response object reference. For `ToListAsync()` without `Limit`, the provider follows all continuation tokens internally; by the time `ToListAsync` returns, those tokens have already been consumed. Use `ToPageAsync` when you need explicit cursor control across requests.

## See also

- [Ordering and Limiting](ordering-limiting.md)
- [How Queries Execute](how-queries-execute.md)
- [Supported Operators](operators.md)
