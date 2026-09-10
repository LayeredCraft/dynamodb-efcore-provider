# Diagnostics, Security, and Limits

## Diagnose behavior

Use normal EF Core logging to inspect generated PartiQL, request execution, write operations,
scan-like query warnings, index selection, and capacity data. Query-compilation events can appear
once per query shape because EF Core caches compiled queries.

Use AWS SDK OpenTelemetry instrumentation for DynamoDB request spans. A paged query can create one
SDK request span per page, and retries can create more.

## Command interception

Provider command interceptors observe query-page and write SDK calls. They cannot suppress calls,
replace responses, or configure retries. Request and response objects can contain PartiQL,
parameter values, keys, and item data; do not mutate them or log sensitive values.

## Limits to expose in application design

- DynamoDB's evaluated-item and 1 MB page boundaries can produce partial pages.
- Queries, writes, batch operations, and transactions have DynamoDB-specific size and count limits.
- Database access is asynchronous only.
- Lifecycle support and schema validation are not a replacement for production infrastructure.

When exact limits affect correctness or cost, verify the current DynamoDB documentation before
shipping.
