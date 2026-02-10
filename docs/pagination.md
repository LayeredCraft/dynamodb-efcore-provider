# Pagination

## Core model
- Result limit and page size are different concepts in this provider.
- Result limit controls how many rows are returned to EF.
- Page size controls how many items DynamoDB evaluates per request.

### Result limit
- Set by row-limiting operators such as `Take(n)`, `FirstAsync`, and `FirstOrDefaultAsync`.
- Enforced by the provider while enumerating query results.

### Page size
- Mapped to DynamoDB `ExecuteStatementRequest.Limit`.
- Controls request evaluation size, not final result count.
- A page can return zero matches and still return `NextToken`.

## DynamoDB ExecuteStatement semantics
- `Limit` is the maximum number of items DynamoDB evaluates, not the number of matching rows returned.
- A read response can stop when DynamoDB reaches the request `Limit` or when it reaches the 1 MB processed-data cap.
- Filtering and matching happen after evaluation/page boundaries, so selective filters can require multiple requests.
- This provider continues pagination using `NextToken` unless `WithoutPagination()` is used.

## Controls
- `WithPageSize(int)`: per-query page size override.
- `DefaultPageSize(int)`: global default page size.
- `WithoutPagination()`: single-request mode.

## Resolution order
1. Per-query `WithPageSize(...)` (last call wins).
2. Global `DefaultPageSize(...)`.
3. `null` (no explicit request limit; DynamoDB page size defaults still apply, including the 1 MB processed-data cap).

## Notes
- `Take` and `First*` are result-limiting operators.
- If row limiting runs without a page size, a warning is logged.
- The provider does not emit SQL `LIMIT`; it stops after enough rows are returned.
- `WithoutPagination()` can return incomplete results for selective predicates.

## Example

```csharp
var items = await db.SimpleItems
    .WithPageSize(25)
    .Where(x => x.Pk == "ITEM#1" && x.BoolValue)
    .Take(3)
    .ToListAsync();
```

- Effective result limit is `3`.
- Effective request page size is `25`.

## Tests that cover this
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/SimpleTable/PaginationTests.cs`
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/SimpleTable/CompiledQueryPaginationTests.cs`
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/PkSkTable/FirstTests.cs`

## Implementation anchors
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/Expressions/SelectExpression.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.QueryingEnumerable.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Storage/DynamoClientWrapper.cs`

## External references
- AWS ExecuteStatement API: <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_ExecuteStatement.html>
