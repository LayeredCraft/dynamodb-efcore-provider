# Query Operators and DynamoDB Behavior

This document is a living reference for how LINQ operators behave in this provider. Each operator
section includes how it translates today and the DynamoDB or provider-specific limitations you
should keep in mind. Add to these sections as support expands.

## How to maintain this page
When an operator behavior changes, update this page in the same story as the code and tests.

## At a glance

### Supported today
- `Where`
- `Select`
- `OrderBy` / `OrderByDescending`
- `ThenBy` / `ThenByDescending`
- `Take`
- `First` / `FirstOrDefault`
- `WithPageSize`
- `WithoutPagination`

### Not supported today
- `Single` / `SingleOrDefault`
- `Any`, `All`, `Count`, `LongCount`
- `Skip`
- `GroupBy`
- `Join` / `GroupJoin` / `SelectMany`
- Complex method-call translation in predicates (for example `ToUpper()` in `Where`)

## Operator matrix (current contract)

| Operator | Server translation | Client behavior | Notes |
| --- | --- | --- | --- |
| `Where` | PartiQL `WHERE` | N/A | Boolean members normalize to `= TRUE` |
| `Select` | Explicit projection list | Some computed expressions run client-side | No `SELECT *` |
| `OrderBy` / `ThenBy` | PartiQL `ORDER BY` | N/A | Precedence and parentheses preserved |
| `Take(n)` | Sets result limit expression | Stops after `n` results | Does not emit SQL `LIMIT` |
| `First*` | Sets result limit `1` | Stops after first result | May scan multiple pages unless pagination disabled |
| `WithPageSize(n)` | Sets request `Limit` | N/A | Last call wins |
| `WithoutPagination()` | Single request only | Stops after first page | Can return incomplete results |

## General paging model
- Result limit (how many results are returned) is separate from page size (how many items DynamoDB
  evaluates per request).
- DynamoDB `Limit` controls evaluation, not returned matches. A page can return zero matches and
  still include `NextToken` if more items could match.
- Responses are capped at ~1MB; paging may be required even with small limits.
- The provider does not emit SQL `LIMIT`; it stops after enough results are returned while request
  `Limit` is used as page size.
- The provider logs a warning when a row-limiting query (`First*`, `Take`) runs without a configured
  page size (`WithPageSize` or `DefaultPageSize`).

## Where
**Purpose**
- Filter results by predicate.

**Translation**
- Translated to PartiQL `WHERE`.
- Boolean members are normalized to explicit comparisons (e.g., `IsActive` becomes `IsActive = TRUE`).

**Limitations / DynamoDB quirks**
- Filters may return zero matches on a page even when more matches exist on later pages.

## Select
**Purpose**
- Shape the projection.

**Translation**
- Translated to explicit `SELECT <projection>`; no `SELECT *` is emitted.

**Limitations / DynamoDB quirks**
- Projection is explicit to keep attribute reads predictable and aligned with type mapping.

## OrderBy / ThenBy
**Purpose**
- Order results by one or more keys.

**Translation**
- Translated to PartiQL `ORDER BY` with `ASC`/`DESC`.

**Limitations / DynamoDB quirks**
- Ordering semantics depend on DynamoDB access patterns and query shape.

## Take(n)
**Purpose**
- Return at most `n` results.

**Translation**
- Sets a result limit of `n` and stops after returning `n` items.
- Page size comes from `.WithPageSize(...)` or `DefaultPageSize` if configured.
- Composes with other `Take`/`First*` operators using the minimum effective limit.

**Limitations / DynamoDB quirks**
- `n` does not imply DynamoDB request `Limit` is `n`.
- If filters are present, multiple pages may be needed to collect `n` matches.
- Without a configured page size, DynamoDB may return a large first page and the provider will log a warning.

## First
**Purpose**
- Return the first result; throw if none exist.

**Translation**
- Sets a result limit of `1` and stops after returning the first item.
- Page size comes from `.WithPageSize(...)` or `DefaultPageSize` if configured.
- If a prior `Take(n)` is applied, the effective result limit is still `1`.

**Limitations / DynamoDB quirks**
- An empty page does not prove absence if `NextToken` is present.
- Without a configured page size, DynamoDB may return a large first page and the provider will log a warning.

## FirstOrDefault
**Purpose**
- Return the first result or `null` if none exist.

**Translation**
- Sets a result limit of `1` and stops after returning the first item.
- Page size comes from `.WithPageSize(...)` or `DefaultPageSize` if configured.
- If a prior `Take(n)` is applied, the effective result limit is still `1`.

**Limitations / DynamoDB quirks**
- An empty page does not prove absence if `NextToken` is present.
- Without a configured page size, DynamoDB may return a large first page and the provider will log a warning.

## WithPageSize(int)
**Purpose**
- Override request page size for a specific query.

**Translation**
- Sets DynamoDB request `Limit` (items evaluated per request).
- If `WithPageSize` is chained multiple times, the last call wins.

**Example**
```csharp
var results = await db.Items
    .WithPageSize(10)
    .Where(item => item.IsActive)
    .WithPageSize(25)
    .ToListAsync();
// Effective page size: 25
```

**Limitations / DynamoDB quirks**
- Smaller page sizes can increase round trips under filtering.

## WithoutPagination()
**Purpose**
- Force a single request for a query.

**Translation**
- Stops after the first DynamoDB request, even if `NextToken` is returned.

**Limitations / DynamoDB quirks**
- Best-effort mode: may return incomplete results when more matches exist on later pages.

## Tests that cover this
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/SimpleTable/WhereTests.cs`
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/SimpleTable/OperatorPrecedenceTests.cs`
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/SimpleTable/SelectTests.cs`
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/SimpleTable/PaginationTests.cs`
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/PkSkTable/FirstTests.cs`

## Implementation anchors
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoSqlTranslatingExpressionVisitor.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/Expressions/SelectExpression.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.QueryingEnumerable.cs`
