---
title: Ordering and Limiting
description: How OrderBy and Limit(n) are handled in DynamoDB queries, and why Take is not supported.
---

# Ordering and Limiting

_DynamoDB enforces that `ORDER BY` columns must be key attributes, and that result limiting controls how many items DynamoDB *evaluates* — not how many matching rows are returned._

## Ordering Results

`OrderBy`/`OrderByDescending` translate to a PartiQL `ORDER BY` clause. `ThenBy`/`ThenByDescending` chain additional sort columns. Any combination of ascending and descending direction is valid.

```csharp
db.Orders
    .Where(o => o.CustomerId == customerId)
    .OrderBy(o => o.CustomerId)
    .ThenByDescending(o => o.CreatedAt)
    .ToListAsync(cancellationToken);
// ORDER BY "CustomerId" ASC, "CreatedAt" DESC
```

!!! warning "Ordering constraint"

    Only the partition key and sort key attributes may appear in `ORDER BY`. Non-key columns throw an `InvalidOperationException` at query compilation. `ORDER BY` also requires a partition-key equality constraint in `WHERE`; open-ended scans without a key condition cannot use ordering.

### Single-Partition vs Multi-Partition Ordering

The rules differ slightly depending on whether the query targets one partition or multiple:

- **Single-partition** (`WHERE pk = value`): order by the partition key, the sort key, or both in any combination.
- **Multi-partition** (`WHERE pk IN (...)`): the partition key must be the *first* column in `ORDER BY`. The sort key may follow.

```csharp
// Single-partition — any key order works
db.Orders
    .Where(o => o.CustomerId == "CUSTOMER#42")
    .OrderByDescending(o => o.CreatedAt)   // sort key only — valid
    .ToListAsync(cancellationToken);

// Multi-partition — partition key must lead
var customers = new[] { "CUSTOMER#1", "CUSTOMER#2" };

db.Orders
    .Where(o => customers.Contains(o.CustomerId))
    .OrderBy(o => o.CustomerId)            // PK first — valid
    .ThenBy(o => o.CreatedAt)
    .ToListAsync(cancellationToken);

// ❌ Invalid for multi-partition: sort key without PK leading
db.Orders
    .Where(o => customers.Contains(o.CustomerId))
    .OrderBy(o => o.CreatedAt)             // throws — PK must come first
    .ToListAsync(cancellationToken);
```

## Limiting Results

`Take(n)` is not supported — use `.Limit(n)` instead.

`.Limit(n)` maps directly to `ExecuteStatementRequest.Limit`. It sets an *evaluation budget*: DynamoDB reads up to `n` items, applies any filter expressions, and returns whatever matches. The returned item count is anywhere from 0 to `n`, depending on how many evaluated items pass the `WHERE` predicate.

```csharp
// Evaluate up to 25 items; apply IsActive filter; return 0–25 results.
var results = await db.Orders
    .Where(o => o.IsActive)
    .Limit(25)
    .ToListAsync(cancellationToken);
```

`Limit(n)` is a single-request operation — the provider does not follow `NextToken` for a limited query. When `.Limit(n)` is chained multiple times, the last call wins.

```csharp
// Effective limit is 20
await db.Orders.Limit(10).Limit(20).ToListAsync(cancellationToken);
```

`n` must be a positive integer. A constant zero or negative value throws `ArgumentOutOfRangeException` when the query is constructed; a runtime-valued `n` that resolves to non-positive throws at execution.

`Limit(n)` is supported in compiled queries with runtime parameters:

```csharp
var query = EF.CompileAsyncQuery(
    (OrderDbContext ctx, int n) => ctx.Orders.Limit(n));

var results = await query(db, 50).ToListAsync(cancellationToken);
```

## Constraints and Caveats

!!! warning

    `Limit(n)` controls how many items DynamoDB *reads*, not how many matching items are returned. With a selective `Where` filter, a `Limit(25)` query may return 0–25 results even when many more matching items exist beyond the evaluated range. If you need all matching items up to a certain count, paginate with `ToPageAsync` until `NextToken` is `null`, or use `ToListAsync()` without `Limit`.

!!! note "Continuation tokens"

    A `Limit(n)` query does not expose a continuation token automatically. If you want to resume from the position where `Limit(n)` stopped, retrieve the response cursor via `db.Entry(item).GetExecuteStatementResponse()?.NextToken` and seed the next query with `.WithNextToken(token)`. See [Pagination](pagination.md) for the full cursor model.

!!! warning "First / FirstOrDefault with Limit"

    `First` / `FirstOrDefault` on a key-only query shape sets an implicit `Limit=1` automatically. Combining an explicit `Limit(n)` with `First*` is not supported and throws at translation time — use `.AsAsyncEnumerable().FirstOrDefaultAsync()` instead.

## See also

- [Pagination](pagination.md)
- [Supported Operators](operators.md)
- [Filtering](filtering.md)
- [Limitations](../limitations.md)
