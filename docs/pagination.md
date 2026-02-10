# Pagination

## Core model
- Result limit and page size are different concepts in this provider.
- Result limit controls how many rows are returned to EF.
- Page size controls how many items DynamoDB evaluates per request.

## Controls
- `WithPageSize(int)`: per-query page size override.
- `DefaultPageSize(int)`: global default page size.
- `WithoutPagination()`: single-request mode.

## Notes
- `Take` and `First*` are result-limiting operators.
- If row limiting runs without a page size, a warning is logged.
