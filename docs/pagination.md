---
icon: lucide/file-stack
---

# Pagination and Evaluation Budget

## Model overview

This provider separates **evaluation budget** (how many items DynamoDB reads) from **terminal behavior**
(what the query returns). There is no general-purpose result limit; instead, use `Limit(n)` to control
the evaluation budget per query.

### Evaluation budget: `Limit(n)`

- `.Limit(n)` maps directly to `ExecuteStatementRequest.Limit`.
- DynamoDB evaluates at most `n` items, applies any non-key filters, and returns 0..n results.
- Always a **single request**. No paging.
- `n` must be a positive integer. Zero or negative throws `ArgumentOutOfRangeException`.
- When chained multiple times, the **last call wins**.

### `First*` terminals

- **Safe path** (PK equality + key-only predicates): sets `Limit=1` implicitly. Single request.
- **Unsafe path** (non-key predicate, scan-like, or user `Limit(n)` present): translation fails. Use `.AsAsyncEnumerable()` instead.

### `ToListAsync()` without `Limit(n)`

- No `Limit` set on request. DynamoDB pages up to 1MB per request.
- Provider follows `NextToken` until exhausted — collects all results.

### `ToListAsync()` with `Limit(n)`

- Single request, `Limit = n`. No paging.

## Unsafe First\* — use AsAsyncEnumerable()

When a `First*` query cannot use the safe key-only path (non-key predicate, scan-like, or any
`Limit(n)` present), translation fails with `InvalidOperationException`. The correct pattern is to
fetch the server-side results then select client-side:

```csharp
// Unsafe shape — non-key filter: use AsAsyncEnumerable()
var active = await db.Orders
    .Where(x => x.UserId == userId && x.IsActive)
    .Limit(50)
    .AsAsyncEnumerable()
    .FirstOrDefaultAsync(cancellationToken);
```

This is the standard EF Core pattern for explicit client-side evaluation: `AsAsyncEnumerable()`
marks where server-side evaluation ends and LINQ-to-objects begins.

## No global default

There is no `DefaultPageSize` option. Use `.Limit(n)` per query.

## Evaluation budget reference

| Shape                                          | `ExecuteStatementRequest.Limit` | Pages? |
| ---------------------------------------------- | ------------------------------- | ------ |
| `.Limit(n)` + `ToListAsync()`                  | `n`                             | No     |
| `First*` (key-only, no explicit limit)         | `1`                             | No     |
| `First*` + any `Limit(n)`                      | **Translation failure**         | —      |
| `First*` on non-key/scan-like path             | **Translation failure**         | —      |
| `.Limit(n)` + `AsAsyncEnumerable()` + `First*` | `n` (client-side selection)     | No     |
| `ToListAsync()` (no limit)                     | `null` (1MB per page)           | Yes    |

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

### Compiled query with runtime parameter

```csharp
var query = EF.CompileAsyncQuery((OrderDbContext ctx, int n)
    => ctx.Orders.Limit(n));

var results = await query(db, 50).ToListAsync(cancellationToken);
```

## External references

- AWS ExecuteStatement API: <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_ExecuteStatement.html>
- DynamoDB PartiQL SELECT: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.select.html>
