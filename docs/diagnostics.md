---
title: Diagnostics and Logging
description: What the DynamoDB EF Core provider logs and how to enable diagnostics.
icon: lucide/activity
---

# Diagnostics and Logging

_The provider integrates with EF Core's standard logging infrastructure, emitting structured
events for query execution, write operations, and index-selection decisions. No provider-specific
configuration is required — standard `Microsoft.Extensions.Logging` wiring is all you need._

## Enabling Logging

The provider hooks into whatever `ILoggerFactory` EF Core has available. In an ASP.NET Core
application this is automatic: the framework registers a logger factory and EF Core picks it up
from the DI container.

Filter to the `Microsoft.EntityFrameworkCore` category to see provider events without noise from
the rest of the framework:

```json title="appsettings.Development.json"
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

To see index-selection decisions (the `DYNAMO_IDX*` codes), the
`Microsoft.EntityFrameworkCore.Query` sub-category must be at `Information` or lower. The
`Microsoft.EntityFrameworkCore` prefix covers this sub-category, so the filter above is sufficient.

**Quick development shortcut** — wire logging directly on the `DbContextOptionsBuilder` without
touching the DI container:

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    optionsBuilder
        .UseDynamo(...)
        .LogTo(Console.WriteLine, LogLevel.Information);
}
```

`LogTo` accepts any `Action<string>` and applies a minimum level filter. It is meant for
development and test contexts, not production deployments.

## What Gets Logged

The provider uses two EF Core logger categories:

| Category           | Full Name                                        | Purpose                            |
| ------------------ | ------------------------------------------------ | ---------------------------------- |
| `Database.Command` | `Microsoft.EntityFrameworkCore.Database.Command` | Query and write execution          |
| `Query`            | `Microsoft.EntityFrameworkCore.Query`            | Index selection during compilation |

**Command events** fire at runtime, once per query execution or once per write statement.

**Query events** fire at *query compilation time* — which, due to EF Core's compiled-query cache,
means once per unique query shape, not once per execution. If you run the same LINQ expression
1000 times you will see the index-selection events once, on the first compilation. This is
intentional: compilation is the expensive decision point, and repeating the log on every execution
would obscure patterns in long-running processes.

Model validation errors and LINQ translation failures are thrown as exceptions and are not
emitted as log events.

## Event Reference

| Event Name                                | Event ID | Category           | Level       | Code            |
| ----------------------------------------- | -------- | ------------------ | ----------- | --------------- |
| `ExecutingPartiQlQuery`                   | 30100    | `Database.Command` | Information | —               |
| `ExecutingExecuteStatement`               | 30101    | `Database.Command` | Information | —               |
| `ExecutedExecuteStatement`                | 30102    | `Database.Command` | Information | —               |
| `ExecutingPartiQlWrite`                   | 30110    | `Database.Command` | Information | —               |
| `NoCompatibleSecondaryIndexFound`         | 30104    | `Query`            | Warning     | `DYNAMO_IDX001` |
| `MultipleCompatibleSecondaryIndexesFound` | 30105    | `Query`            | Warning     | `DYNAMO_IDX002` |
| `SecondaryIndexSelected`                  | 30106    | `Query`            | Information | `DYNAMO_IDX003` |
| `ExplicitIndexSelected`                   | 30107    | `Query`            | Information | `DYNAMO_IDX004` |
| `SecondaryIndexCandidateRejected`         | 30108    | `Query`            | Information | `DYNAMO_IDX005` |
| `ExplicitIndexSelectionDisabled`          | 30109    | `Query`            | Information | `DYNAMO_IDX006` |
| `ScanLikeQueryDetected`                   | 30111    | `Query`            | Warning     | —               |

Event IDs are stable across releases. You can use them to filter log output programmatically or
in structured logging sinks.

## Command Events

Command events cover the full lifecycle of a query or write reaching DynamoDB: the PartiQL text
that was generated, the request parameters sent to the `ExecuteStatement` API, and the response
metadata returned.

### `ExecutingPartiQlQuery` — 30100

Fires once per query execution, immediately before the first `ExecuteStatement` HTTP call. Emits
the table name and the full PartiQL statement that will be sent:

```
info: Microsoft.EntityFrameworkCore.Database.Command[30100]
      Executing DynamoDB PartiQL query for table 'Orders'
      SELECT "PK", "SK", "Status", "Total" FROM "Orders"
      WHERE "PK" = ?
```

This is the primary tool for verifying LINQ translation. If you see a full table scan when you
expected a targeted key lookup, this event tells you what PartiQL was produced and confirms
whether the WHERE clause reflects your intent.

### `ExecutingExecuteStatement` — 30101

Fires immediately before each HTTP call to DynamoDB. A single query may produce multiple
`ExecutingExecuteStatement` events if the result spans more than one DynamoDB page:

```
info: Microsoft.EntityFrameworkCore.Database.Command[30101]
      Executing DynamoDB ExecuteStatement request (limit: 25, nextTokenPresent: False, seedNextTokenPresent: False)
```

Fields:

| Field                  | Meaning                                                                                                                        |
| ---------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| `limit`                | The page size sent on this request. `null` means no limit was set — DynamoDB will return up to its internal 1 MB cap per page. |
| `nextTokenPresent`     | `True` when this is a continuation call — the provider is fetching the next page using a token from the previous response.     |
| `seedNextTokenPresent` | `True` when the *first* request used a user-supplied pagination token (e.g. from a manual pagination call).                    |

The `limit` value is set by a query-level `Limit(n)` call or by `ToPageAsync(limit, token)`. If it
is `null` and your query can return many items, you will typically see multiple round-trips as the
provider auto-pages through DynamoDB results.

### `ExecutedExecuteStatement` — 30102

Fires after each HTTP response, paired with the preceding `ExecutingExecuteStatement`:

```
info: Microsoft.EntityFrameworkCore.Database.Command[30102]
      Executed DynamoDB ExecuteStatement request (itemsCount: 25, nextTokenPresent: True)
```

## Query Events

### `ScanLikeQueryDetected` — 30111

Fires when `DynamoEventId.ScanLikeQueryDetected` is configured to log and a scan-like read continues:

```
warn: Microsoft.EntityFrameworkCore.Query[30111]
      Scan-like DynamoDB query detected for table 'Orders' on base table: missing equality predicate on partition key 'PK'. Add an equality predicate on the active partition key and at most one sort-key key condition, configure ConfigureWarnings for DynamoEventId.ScanLikeQueryDetected, or append .AllowScan() for an intentional per-query scan.
```

By default, the same message is thrown as an `InvalidOperationException` before PartiQL generation or `ExecuteStatement`. Use `ConfigureWarnings` to `Log`, `Ignore`, or `Throw` this event.

Fields:

| Field              | Meaning                                                                      |
| ------------------ | ---------------------------------------------------------------------------- |
| `itemsCount`       | Number of items in this page.                                                |
| `nextTokenPresent` | `True` when DynamoDB returned a continuation token — more data is available. |

If `itemsCount` is less than `limit` and `nextTokenPresent` is still `True`, DynamoDB reached its
internal 1 MB page cap before filling your requested page size. The provider will automatically
issue another request.

### `ExecutingPartiQlWrite` — 30110

Fires for each write operation (INSERT, UPDATE, or DELETE) before it is sent to DynamoDB:

```
info: Microsoft.EntityFrameworkCore.Database.Command[30110]
      Executing DynamoDB PartiQL write for table 'Orders'
      INSERT INTO "Orders" VALUE {'PK': ?, 'SK': ?, 'Status': ?, 'Total': ?}
```

This event fires per-statement, not per-batch. When a `SaveChangesAsync` call produces multiple
write operations — whether transactional or non-transactional — each one generates its own
`ExecutingPartiQlWrite` event before the batch is submitted.

Use this event to verify that complex properties, primitive collections, and discriminator values are
being serialized into the expected PartiQL parameter positions.

## Index Selection Events

These events are emitted by the query compiler during index-selection analysis. Because they fire
at compilation time, they appear once per unique query shape, not once per execution.

Index selection runs when [automatic index selection](querying/index-selection.md) is enabled via
`DynamoAutomaticIndexSelectionMode`. Setting the mode to `Off` suppresses all `DYNAMO_IDX*`
events except `DYNAMO_IDX004` (explicit `.WithIndex(...)` hints) and `DYNAMO_IDX006`
(explicit `.WithoutIndex()` hints), which always fire.

When multiple candidates are evaluated, `DYNAMO_IDX005` rejection events appear first, followed by
the final outcome event (`DYNAMO_IDX001`, `DYNAMO_IDX002`, or `DYNAMO_IDX003`). Enable
`Information` level logging for the `Query` category to see the full decision trail.

______________________________________________________________________

### `DYNAMO_IDX001` — `NoCompatibleSecondaryIndexFound` (Warning)

No secondary index on the queried table satisfies the predicate. The query will use the base
table.

This is a warning rather than an informational event because a base-table query may scan more data
than you intended. The message also reminds you to ensure the WHERE clause includes an equality
constraint on an index partition key if you want automatic selection to pick up a GSI. Review
whether a GSI would allow the provider to target a narrower key space, and confirm that running
against the base table is acceptable for this query's access pattern.

______________________________________________________________________

### `DYNAMO_IDX002` — `MultipleCompatibleSecondaryIndexesFound` (Warning)

Multiple secondary indexes on the table are equally suitable. The message lists the tied index
names. The query falls back to the base table.

Use an explicit [`.WithIndex("IndexName")`](querying/index-selection.md) hint to resolve the
ambiguity. The tied index names appear in the message, so you can pick the one that best fits the
access pattern.

______________________________________________________________________

### `DYNAMO_IDX003` — `SecondaryIndexSelected` (Information)

Automatic selection chose a single compatible index. The query will be rewritten to target it.
Confirm that the selected index and table match your intent:

```
info: Microsoft.EntityFrameworkCore.Query[30106]
      Index 'ByStatus-index' on table 'Orders' was auto-selected.
```

In `On` mode the message reads "was auto-selected" and the query is rewritten to target the index.
In `SuggestOnly` mode the message reads "would be auto-selected if automatic index selection were
On" and the query is not rewritten — useful for auditing which indexes would fire without changing
production query routing.

______________________________________________________________________

### `DYNAMO_IDX004` — `ExplicitIndexSelected` (Information)

The query used an explicit `.WithIndex("IndexName")` hint and the provider resolved it
successfully:

```
info: Microsoft.EntityFrameworkCore.Query[30107]
      Index 'ByStatus-index' on table 'Orders' was explicitly selected via WithIndex().
```

______________________________________________________________________

### `DYNAMO_IDX005` — `SecondaryIndexCandidateRejected` (Information)

A candidate index was evaluated and eliminated. The rejection reason is included in the message:

```
info: Microsoft.EntityFrameworkCore.Query[30108]
      Index 'ByCategory-index' on table 'Orders' was rejected: no equality or IN constraint on the index partition key.
```

Common rejection reasons:

- No equality or `IN` constraint on the index partition key
- Predicate contains an unsafe `OR` that would produce incorrect results
- Index projection type is not `ALL` (only ALL projection is auto-selected)

These events appear before the final outcome event and give you visibility into why specific
indexes were ruled out.

______________________________________________________________________

### `DYNAMO_IDX006` — `ExplicitIndexSelectionDisabled` (Information)

The query used `.WithoutIndex()` to suppress automatic index selection. The query will run
against the base table regardless of matching indexes:

```
info: Microsoft.EntityFrameworkCore.Query[30109]
      Index selection was suppressed for table 'Orders' by '.WithoutIndex()'. The query will execute against the base table.
```

## Interpreting Pagination Logs

Each DynamoDB page produces a before/after pair of `ExecutingExecuteStatement` and
`ExecutedExecuteStatement` events. Reading the pairs together tells you exactly what happened
on each round-trip.

**Single page — query returned all results in one call:**

```
info: ...Database.Command[30100]
      Executing DynamoDB PartiQL query for table 'Orders'
      SELECT * FROM "Orders" WHERE "PK" = ?

info: ...Database.Command[30101]
      Executing DynamoDB ExecuteStatement request (limit: 25, nextTokenPresent: False, seedNextTokenPresent: False)

info: ...Database.Command[30102]
      Executed DynamoDB ExecuteStatement request (itemsCount: 18, nextTokenPresent: False)
```

18 items came back, no continuation token — done in one round-trip.

**Multiple pages — DynamoDB hit its 1 MB internal cap:**

```
info: ...Database.Command[30101]
      Executing DynamoDB ExecuteStatement request (limit: 25, nextTokenPresent: False, seedNextTokenPresent: False)

info: ...Database.Command[30102]
      Executed DynamoDB ExecuteStatement request (itemsCount: 12, nextTokenPresent: True)

info: ...Database.Command[30101]
      Executing DynamoDB ExecuteStatement request (limit: 25, nextTokenPresent: True, seedNextTokenPresent: False)

info: ...Database.Command[30102]
      Executed DynamoDB ExecuteStatement request (itemsCount: 6, nextTokenPresent: False)
```

The first page returned only 12 items even though the limit was 25 — DynamoDB evaluated more
than 1 MB of data and stopped early. The provider fetched the continuation automatically and got
the remaining 6 items. Total: 18 items in two round-trips.

If you see many round-trips with `nextTokenPresent: True`, your filter is very selective relative
to the data stored in DynamoDB (or the index). Either apply `Limit(n)` / `ToPageAsync(limit, ...)`
to bound per-request evaluation, or restructure the query to target a narrower key prefix.

## Response Metadata

After a **tracking query** executes, the raw `ExecuteStatementResponse` from each DynamoDB page
is accessible on the entity entry. No-tracking queries do not populate this.

```csharp
var order = await context.Orders
    .Where(x => x.Pk == "USER#42" && x.Sk == "ORDER#7")
    .FirstAsync();

var response = context.Entry(order).GetExecuteStatementResponse();

// Always populated — include in AWS support tickets and distributed traces
var requestId = response?.ResponseMetadata.RequestId;
logger.LogInformation("DynamoDB request {RequestId}", requestId);

// Only populated when ReturnConsumedCapacity was set on the request
var capacity = response?.ConsumedCapacity;
```

### Page semantics

- Entities from the **same DynamoDB page** share the **same response object reference**.
- Entities from **different pages** hold **different response objects**.
- The `NextToken` on a non-last-page response has already been consumed by the provider for
    internally auto-paged queries — do not use it for manual pagination.

These semantics let you correlate read cost and request ID per page when your query returns
results from multiple round-trips.

### `ConsumedCapacity`

`ConsumedCapacity` is `null` unless `ReturnConsumedCapacity` was configured on the underlying
`ExecuteStatementRequest`. To set this today, access the low-level client directly:

```csharp
var client = context.Database.GetDynamoClient();
// Use client.ExecuteStatementAsync(...) with ReturnConsumedCapacity configured
```

!!! note "Planned"

    Provider-level configuration for `ReturnConsumedCapacity` is tracked for a future release.

## Model Validation Errors

Model validation runs during `DbContext` initialization, before any queries execute. Failures
throw exceptions — they are not emitted as EF Core log events.

| Validation failure                                | Likely cause                                                                       | Fix                                                                                               |
| ------------------------------------------------- | ---------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| Shared-table key shape mismatch                   | Entity types on the same table disagree on PK-only vs PK+SK                        | Ensure all entities on the table have matching key configurations                                 |
| Shared-table key attribute name mismatch          | PK or SK attribute names differ across co-located entity types                     | Use consistent attribute name overrides across all entity types                                   |
| Key type category mismatch                        | Same attribute is mapped as `string` on one type and `number` on another           | Align value converters so all types agree on the store type                                       |
| Nullable key property                             | A partition key or sort key property is nullable                                   | DynamoDB key attributes must be non-null; make the property required                              |
| Non-DynamoDB-compatible key type                  | Key property maps to `bool`, `Guid`, or another unsupported store type             | Add a `ValueConverter` to map to `string`, a number type, or `byte[]`                             |
| Secondary-index key type incompatible             | GSI/LSI key property resolves to an unsupported type                               | Same as above; applies independently for each index key property                                  |
| Root entity uses `HasKey(...)` or `[Key]`         | EF Core primary key was declared instead of DynamoDB key mapping                   | Replace with `HasPartitionKey(...)` and `HasSortKey(...)`                                         |
| Local secondary index without sort key            | `HasLocalSecondaryIndex(...)` on a table that has no sort key                      | LSIs require the table to define both a partition key and a sort key                              |
| Duplicate discriminator values                    | Two entity types on the same table share the same discriminator value              | Assign a unique discriminator value to each type                                                  |
| Discriminator attribute name conflicts with PK/SK | The discriminator attribute name collides with the partition or sort key attribute | Change the discriminator name via `HasEmbeddedDiscriminatorName(...)` or rename the key attribute |

See [Entities and Keys](modeling/entities-keys.md) and [Secondary Indexes](modeling/secondary-indexes.md)
for configuration details.

## Translation Failures

Unsupported LINQ operators throw `InvalidOperationException` during query translation — before any
DynamoDB requests are sent. The exception message includes provider-specific details about the
unsupported shape (unsupported aggregates, joins, set operations, offset-style operators, etc.).

Translation failures are not emitted as log events. They propagate as exceptions and surface
during development, not in production steady-state.

See [Supported Operators](querying/operators.md) and [Limitations](limitations.md) for the full
list of supported and unsupported LINQ shapes.

## Not Yet Available

The following diagnostic features are tracked for future releases:

**`ConfigureWarnings` integration** — Warning events (`DYNAMO_IDX001`, `DYNAMO_IDX002`) cannot
yet be escalated to exceptions or suppressed via `optionsBuilder.ConfigureWarnings(w => w.Throw(...))`.
Standard EF Core warning configuration has no effect on these events today.

**Sensitive data logging** — `EnableSensitiveDataLogging()` has no effect on DynamoDB provider
logs. Parameter values in PartiQL statements are always omitted from log output regardless of
this setting.

**Row-limiting query warning** — A warning for queries that can return many rows but have no
explicit `Limit(n)` is planned but not yet emitted. Use the `ExecutedExecuteStatement` events to
monitor round-trip counts and item counts in the meantime.

## See Also

- [Configuration → DbContext Options](configuration/dbcontext.md)
- [Querying → Index Selection](querying/index-selection.md)
- [Querying → How Queries Execute](querying/how-queries-execute.md)
- [Querying → Pagination](querying/pagination.md)
- [AWS ExecuteStatement API reference](https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_ExecuteStatement.html)
