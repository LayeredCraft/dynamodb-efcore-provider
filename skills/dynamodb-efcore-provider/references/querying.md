# Querying and Pagination

LINQ is translated to DynamoDB PartiQL and executed through `ExecuteStatement`. Use
`ToQueryString()` to inspect the generated statement without sending a request.

## Translation boundaries

Supported LINQ is intentionally narrower than relational EF Core. Joins, `GroupBy`, general query
aggregates, `Skip`, `Take`, set operations, and many element operators do not translate. Keep the
database part of a query to supported predicates, projection, ordering, and terminal operators.

Use `AsAsyncEnumerable()` only to make the client-side boundary explicit. It fetches all matching
pages needed by the subsequent in-memory operator, so constrain the server query first.

## Targeted reads, scans, and indexes

- Constrain the active partition key with equality or `IN` for targeted reads.
- A missing targeted partition-key condition is scan-like and throws by default. Use `.AllowScan()`
  only after accepting its cost and latency.
- Use an explicit index hint only when the application knows which index is appropriate. Otherwise,
  use automatic index selection and inspect diagnostics when it chooses unexpectedly.
- `First*` and `Single*` require safe key-condition shapes. A filter can evaluate non-matching
  items before a match, so it is not generally safe for server-limited single-result reads.

## Limits and pages

`Limit(n)` sets DynamoDB's evaluated-item budget. Filters run after evaluation, so a page can have
fewer than `n` matches—or zero matches—and still return a continuation token. Use `ToPageAsync` and
the returned token for deliberate page-by-page application APIs; check token exhaustion, not item
count, to find the end.

## Projection and ordering

Server-side projection reduces data returned from DynamoDB. Computed projection shaping can run
after materialization. Ordering is constrained by DynamoDB key and index rules; do not promise
arbitrary relational ordering.
