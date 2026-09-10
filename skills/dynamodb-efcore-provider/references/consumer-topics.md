# Consumer Topics and Sharp Edges

Read the named public page for the exact API and current support. This map covers every
consumer-facing documentation area without duplicating the docs.

| Topic                                                                                          | Public documentation                                                                |
| ---------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| Installation, requirements, and a complete first context                                       | `docs/index.md`, `docs/getting-started.md`                                          |
| DynamoDB tables, items, keys, indexes, consistency, and no joins                               | `docs/dynamodb-concepts.md`                                                         |
| Client ownership, authentication, regions, DynamoDB Local, retries, and options                | `docs/configuration/client-setup.md`, `docs/configuration/dbcontext.md`             |
| Table creation, validation, deletion, seeding, and health checks                               | `docs/configuration/lifecycle.md`                                                   |
| Attribute names, table/key mapping, generated keys, and validation                             | `docs/configuration/attribute-naming.md`, `docs/configuration/table-key-mapping.md` |
| Entities, complex values, collections, indexes, and shared-table discriminators                | `docs/modeling/`                                                                    |
| Operator support, filtering, scans, projections, ordering, limits, tokens, and index selection | `docs/querying/`                                                                    |
| Generated PartiQL, parameterization, execution, client evaluation, and async access            | `docs/querying/how-queries-execute.md`                                              |
| Adds, updates, deletes, transactions, statement limits, and concurrency                        | `docs/saving/`                                                                      |
| Logging, OpenTelemetry, capacity, events, response metadata, and SDK interception              | `docs/diagnostics.md`                                                               |
| Unsupported features, lifecycle gaps, query safety, writes, modeling, and AOT restrictions     | `docs/limitations.md`                                                               |
| Precompiled queries and NativeAOT                                                              | `docs/querying/precompiled-queries.md`, `docs/limitations.md`                       |

## Check these before recommending code

- DynamoDB does not provide relational joins, foreign-key behavior, offset paging, or server-side
  query aggregation. Confirm LINQ support in `docs/querying/operators.md`.
- A read without a targeted partition-key condition is scan-like and throws by default. Use
  `.AllowScan()` only for intentional scans.
- `First*` and `Single*` need safe key-condition query shapes. Read `docs/limitations.md` before
  recommending them with filters.
- `Limit(n)` is an evaluation budget, not a result count. Continue until the token is null when
  callers need all matches.
- `SaveChanges` never creates tables. Lifecycle calls are async-only, and existing-schema
  validation is limited.
- Transactions have DynamoDB item and statement limits. Updates and writes also have statement-size
  limits; use `docs/saving/transactions.md` and `docs/saving/add-update-delete.md` for the exact
  behavior.
- Configure concurrency tokens and update them manually when required; read
  `docs/saving/concurrency.md`.
- Diagnostics and interceptor request/response data can contain PartiQL, keys, parameters, and
  returned items. Do not log or mutate them carelessly.
- NativeAOT/precompiled queries are restricted. Native publish/run coverage is EF10-only; EF11 has
  generation coverage but not native publish-and-run support.
