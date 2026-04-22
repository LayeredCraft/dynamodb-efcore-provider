---
title: How Queries Execute
description: How LINQ expressions are translated to PartiQL and executed via the DynamoDB ExecuteStatement API.
---

# How Queries Execute

_The provider compiles LINQ expressions into PartiQL `SELECT` statements and executes them using DynamoDB's `ExecuteStatement` API, with any unsupported operators failing at translation time rather than silently falling back to client evaluation._

## LINQ to PartiQL

When you execute a LINQ query, the provider runs it through a multi-stage compilation pipeline before any network call is made:

1. **`DynamoQueryableMethodTranslatingExpressionVisitor`** walks the LINQ expression tree and builds a `SelectExpression` — an internal query model representing the projected columns, `WHERE` predicate, `ORDER BY` orderings, and evaluation limit.
1. **`DynamoQueryTranslationPostprocessor`** finalizes the `SelectExpression`: it injects discriminator predicates for shared-table entity types and runs index selection analysis to determine whether a GSI or LSI should be used.
1. **`DynamoQuerySqlGenerator`** converts the `SelectExpression` into a PartiQL `SELECT` statement with positional `?` placeholders and a matching `AttributeValue` parameter list.

```csharp
var orders = await db.Orders
    .Where(o => o.CustomerId == customerId && o.Status == "PENDING")
    .OrderBy(o => o.CreatedAt)
    .Select(o => new { o.OrderId, o.Total })
    .ToListAsync(cancellationToken);

// Generated PartiQL:
// SELECT "OrderId", "Total"
// FROM "Orders"
// WHERE "CustomerId" = ? AND "Status" = ?
// ORDER BY "CreatedAt" ASC
```

Identifiers are always double-quoted in generated PartiQL, and quoted identifiers are case-sensitive. The parameter values are serialized as `AttributeValue` objects; no literal values appear in the SQL text.

## The ExecuteStatement Model

All queries execute via the DynamoDB [`ExecuteStatement`](https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_ExecuteStatement.html) API — not via the `Query` or `Scan` SDK methods. `ExecuteStatement` accepts a PartiQL SELECT and a list of positional parameters, then returns a page of results and an optional continuation token.

Two fields on the request control result size:

- **`Limit`** — an *evaluation budget*: the number of items DynamoDB reads before applying filter expressions. It is not a guarantee of how many matching items are returned. A `Limit(25)` query may return anywhere from 0 to 25 items depending on how many of the evaluated items satisfy the `WHERE` predicate.
- **`NextToken`** — a continuation cursor returned by DynamoDB when the response was cut short by the `Limit` or by the 1 MB processed-data cap per request. The provider follows `NextToken` automatically for unbounded queries; for paged queries it exposes the token via `ToPageAsync`.

!!! note

    DynamoDB stops processing a request at either the `Limit` item count or 1 MB of processed data, whichever comes first. A page can therefore contain zero matching items while still returning a non-null `NextToken` when many non-matching items were evaluated. Always check `NextToken == null` (not `Items.Count`) to determine end of results.

## Client-Side vs Server-Side Evaluation

The provider has a strict server-side-first policy: if a LINQ operator cannot be translated to PartiQL, the translation fails with an `InvalidOperationException` at compile time. The provider never silently evaluates unsupported operators in-process against a full table scan.

The one sanctioned form of client-side evaluation is *explicit*: calling `.AsAsyncEnumerable()` marks the boundary between server execution and LINQ-to-objects. Any LINQ operators chained after `AsAsyncEnumerable()` run in-process against the result stream the server returned.

```csharp
// Server evaluates the WHERE and Limit; client selects the first match.
var active = await db.Orders
    .Where(o => o.CustomerId == customerId && o.IsActive)
    .Limit(50)
    .AsAsyncEnumerable()
    .FirstOrDefaultAsync(cancellationToken);
```

Some projection shaping also runs client-side — computed expressions in `Select` (string methods, arithmetic, constructor calls) are applied by the shaper lambda after the server returns the raw `AttributeValue` rows. This is expected behavior and is documented per operator on the [Projection](projection.md) page.

## Async Execution

All query execution is async. Attempting to enumerate results synchronously throws `InvalidOperationException`. Use `ToListAsync()`, `FirstOrDefaultAsync()`, `AsAsyncEnumerable()`, or `ToPageAsync()` depending on how you want to consume results.

## See also

- [Supported Operators](operators.md)
- [Filtering](filtering.md)
- [Pagination](pagination.md)
- [Limitations](../limitations.md)
