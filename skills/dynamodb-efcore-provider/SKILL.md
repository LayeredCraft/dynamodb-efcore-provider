---
name: dynamodb-efcore-provider
description: >-
  Use for any task involving the EntityFrameworkCore.DynamoDb provider: consuming it in an
  application, configuring entity/table mappings, writing or debugging LINQ queries and generated
  PartiQL, saving data, diagnosing provider behavior, or changing and testing the provider itself.
  Use this instead of the former DynamoMapper, PartiQL, AOT, documentation, integration-test, and
  specification-test skills.
---

# DynamoDB EF Core Provider

Use this as the single guide for the repository and its consumers. Start with the user's goal, then
read only the matching reference or published doc. Do not load every reference by default.

When the repository is not available, use the current published documentation at
`https://dynamodb-ef-core.layeredcraft.dev/` before relying on general EF Core or DynamoDB advice.

## Start here

Classify the request:

| Request                                                                         | Read first                                                                                                                            |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| Install the package or check .NET/EF compatibility                              | `docs/index.md`, `docs/getting-started.md`                                                                                            |
| Learn DynamoDB terms and relational differences                                 | `docs/dynamodb-concepts.md`, `docs/limitations.md`                                                                                    |
| Configure AWS credentials, a client, DynamoDB Local, or provider options        | `docs/configuration/client-setup.md`, `docs/configuration/dbcontext.md`                                                               |
| Create, validate, delete, seed, or health-check tables                          | `docs/configuration/lifecycle.md`                                                                                                     |
| Set up entities, table keys, indexes, naming, complex values, or a shared table | `docs/modeling/`, `docs/configuration/table-key-mapping.md`                                                                           |
| Write or debug a LINQ query                                                     | `docs/querying/`, then `references/partiql/` for DynamoDB syntax or limits                                                            |
| Use scans, consistency, explicit indexes, ordering, limits, or tokens           | `docs/querying/filtering.md`, `docs/querying/index-selection.md`, `docs/querying/ordering-limiting.md`, `docs/querying/pagination.md` |
| Understand generated PartiQL or execution                                       | `docs/querying/how-queries-execute.md`                                                                                                |
| Save, update, delete, transact, or handle concurrency                           | `docs/saving/`                                                                                                                        |
| Configure logs, tracing, capacity, or SDK interception                          | `docs/diagnostics.md`                                                                                                                 |
| Find supported features or sharp edges                                          | `docs/limitations.md`, `references/consumer-topics.md`                                                                                |
| Use DynamoMapper                                                                | `references/dynamo-mapper/core-usage.md`                                                                                              |
| Work on this provider                                                           | `references/provider-development.md`                                                                                                  |
| Add an end-to-end test                                                          | `references/integration-tests.md`                                                                                                     |
| Change EF Core specification-test coverage                                      | `tests/EntityFrameworkCore.DynamoDb.SpecificationTests/AGENTS.md`                                                                     |
| Change precompiled-query or NativeAOT support                                   | `testapps/EntityFrameworkCore.DynamoDb.NativeAotSmoke/AGENTS.md` and `references/provider-development.md`                             |
| Change EF-version support, packages, CI, or releases                            | `docs/multi-version-ef-strategy.md`, `.github/workflows/`                                                                             |

## Facts that prevent bad advice

- LINQ becomes DynamoDB PartiQL and runs through `ExecuteStatement`; it does not use the SDK
  `Query` or `Scan` operations directly.
- The provider is async-only for database access. Use `ToListAsync`, `FindAsync`, or async
  enumeration; synchronous database querying throws.
- Unsupported LINQ does not silently run on the client. Use `AsAsyncEnumerable()` only when the
  caller deliberately wants a client-side boundary.
- DynamoDB `Limit` is an evaluated-item budget, not a count of matching items. An empty page can
  still have a continuation token.
- Generated PartiQL uses quoted identifiers and positional `?` parameters. Use `ToQueryString()`
  to inspect it without executing a request.
- DynamoDB and this provider have constraints that relational EF Core users may not expect. Read
  `references/consumer-topics.md` and `docs/limitations.md` before promising support for a query,
  relationship, transaction, lifecycle operation, or key shape.

## DynamoMapper

Read only what the question needs:

- normal mapping and attributes: `references/dynamo-mapper/core-usage.md`
- supported CLR types and collections: `references/dynamo-mapper/type-matrix.md`
- hooks: `references/dynamo-mapper/hooks.md`
- compiler diagnostics: `references/dynamo-mapper/diagnostics.md`
- stale documentation and hard limits: `references/dynamo-mapper/gotchas.md`

Keep mapping configuration on a `static partial` mapper class. Do not invent hook or converter
signatures; read the matching reference.

## DynamoDB PartiQL

This provider is limited by DynamoDB's PartiQL subset. Read `references/partiql/overview.md` first,
then load the smallest matching page:

- `statements-select.md`, `statements-insert.md`, `statements-update.md`, or `statements-delete.md`
- `operators.md` or `functions.md` and its named function page
- `transactions.md`, `batch-operations.md`, or `iam.md`
- `data-types.md` or `getting-started.md`

When exact syntax, limits, or AWS behavior matters, verify against current AWS documentation.

## Provider changes

1. Read `AGENTS.md`, `AGENTS.local.md` when present, and the matching test instructions.
2. Start with a failing or new test. Follow the flow in `references/provider-development.md`.
3. Keep user docs and this skill accurate in the same PR when behavior, setup, limits, diagnostics,
   or supported shapes change. If neither needs a change, explain why in the PR template.
4. Before completion, run the required EF10 and EF11 suites. Build docs when docs change.

## Reference map

- `references/provider-development.md` — source/test/doc map and change checklist
- `references/consumer-topics.md` — complete public-doc routing and sharp-edge checklist
- `references/doc-pages.md` — page-impact matrix and operator entry format
- `references/integration-tests.md` — DynamoDB Local test conventions
- `references/dynamo-mapper/` — mapper guidance
- `references/partiql/` — DynamoDB PartiQL guidance
- `evals/evals.json` — starter evaluation prompts for this skill
