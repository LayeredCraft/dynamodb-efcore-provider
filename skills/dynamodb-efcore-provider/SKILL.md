---
name: dynamodb-efcore-provider
description: >-
  Use when an application consumes EntityFrameworkCore.DynamoDb: installing or configuring the
  provider, modeling DynamoDB tables and keys, writing or debugging LINQ queries, handling
  pagination, scans, indexes, writes, transactions, concurrency, diagnostics, DynamoMapper, or
  precompiled-query and NativeAOT constraints. Use this for consumer questions even when the user
  does not name DynamoDB EF Core explicitly.
---

# DynamoDB EF Core Provider

Guide application developers by subject. Load only the matching reference; do not load every file.
The bundled references are the primary guide. For a newer release or exact API details, verify
against `https://dynamodb-ef-core.layeredcraft.dev/`.

## Choose a subject

| Need                                                                                                                            | Read                                                              |
| ------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| Install, register a context, create an AWS client, authenticate, use DynamoDB Local, or configure options                       | `references/setup.md`                                             |
| Design keys, tables, attributes, indexes, complex values, collections, or a shared table                                        | `references/modeling.md`                                          |
| Write LINQ, understand translation, scans, indexes, consistency, projection, ordering, limits, pagination, or generated PartiQL | `references/querying.md`                                          |
| Add, update, delete, transact, configure concurrency, create tables, seed, or add health checks                                 | `references/writes-and-lifecycle.md`                              |
| Enable logs, tracing, capacity data, command interception, or debug a failure                                                   | `references/diagnostics-and-limits.md`                            |
| Use precompiled queries or NativeAOT                                                                                            | `references/aot.md`                                               |
| Use DynamoMapper                                                                                                                | `references/dynamo-mapper/core-usage.md`                          |
| Need DynamoDB PartiQL syntax, functions, transactions, batches, or IAM                                                          | `references/partiql/overview.md`, then the smallest matching page |

## Rules that avoid unsafe advice

- DynamoDB is not relational: no joins, foreign-key behavior, offset paging, or general server-side
  aggregates. Check the querying reference before promising LINQ support.
- Database queries are async-only. Use `ToListAsync`, `FindAsync`, `ToPageAsync`, or async
  enumeration.
- Untranslatable LINQ fails; it does not silently run on the client. Use `AsAsyncEnumerable()` only
  when the application deliberately accepts client-side work over fetched results.
- A missing targeted partition-key condition is scan-like and throws by default. Recommend
  `.AllowScan()` only for intentional scans.
- `Limit(n)` is an evaluated-item budget, not a number of matching results. Use continuation tokens
  when the caller needs complete results.
- Do not invent DynamoMapper hook or converter signatures. Read its focused reference first.
- Do not expose PartiQL, keys, parameters, or returned items through logs, traces, or interceptors.

## Progressive references

- `references/setup.md`
- `references/modeling.md`
- `references/querying.md`
- `references/writes-and-lifecycle.md`
- `references/diagnostics-and-limits.md`
- `references/aot.md`
- `references/dynamo-mapper/`
- `references/partiql/`
