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
- **Safe path + explicit `Limit(n)`**: user limit wins over the implicit `Limit=1`.
- **Unsafe path** (non-key predicate or scan-like): requires `.WithNonKeyFilter()` opt-in. Translation fails without it.

### `ToListAsync()` without `Limit(n)`

- No `Limit` set on request. DynamoDB pages up to 1MB per request.
- Provider follows `NextToken` until exhausted — collects all results.

### `ToListAsync()` with `Limit(n)`

- Single request, `Limit = n`. No paging.

## `WithNonKeyFilter()` opt-in

Required for `First*` on non-safe query shapes:

- **Non-key predicate present**: any attribute outside the partition key or sort key.
- **Scan-like path**: no partition-key equality constraint.
- **PK `IN (...)` constraint**: treated as non-equality.

This is a **permission flag only** — it does not change execution behavior. The caller accepts that
the result may be `null` even when matches exist beyond the evaluation budget.

**Silent no-op** when applied to `ToListAsync()` or key-only `First*` queries.

**Special case — no sort key**: when the queried source has no sort key, each partition contains at
most one item. `First*` with PK equality is always safe regardless of non-key predicates.
`WithNonKeyFilter()` is accepted but not required in this case.

## No global default

There is no `DefaultPageSize` option. Use `.Limit(n)` per query.

## Evaluation budget reference

| API | `ExecuteStatementRequest.Limit` | Pages? |
|---|---|---|
| `.Limit(n)` + `ToListAsync()` | `n` | No |
| `.Limit(n)` + `First*` | `n` | No |
| `First*` (key-only, no explicit limit) | `1` | No |
| `First*` + `.WithNonKeyFilter()` (no explicit limit) | `null` (1MB) | No |
| `First*` + `.WithNonKeyFilter()` + `.Limit(n)` | `n` | No |
| `ToListAsync()` (no limit) | `null` (1MB per page) | Yes |

## Examples

### Evaluation budget on a scan

```csharp
// Evaluate 25 items, apply IsActive filter, return 0..25.
var orders = await db.Orders
    .Where(x => x.IsActive)
    .Limit(25)
    .ToListAsync(cancellationToken);
```

### First* with non-key filter (opt-in required)

```csharp
var active = await db.Orders
    .Where(x => x.UserId == userId && x.IsActive)
    .WithNonKeyFilter()
    .Limit(50)
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
