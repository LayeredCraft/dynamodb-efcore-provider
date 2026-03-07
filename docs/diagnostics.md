---
icon: lucide/stethoscope
---

# Diagnostics

## Logging
- Logs PartiQL command execution.
- Logs `ExecuteStatement` request metadata (for example limit and pagination token presence).
- Logs `ExecuteStatement` response metadata (for example item count and token presence).

### Event IDs
- `ExecutingPartiQlQuery`
- `ExecutingExecuteStatement`
- `ExecutedExecuteStatement`
- `RowLimitingQueryWithoutPageSize`

## Warnings
- Row-limiting query without configured page size logs a warning.

## Translation failures
- Unsupported LINQ operators fail with `InvalidOperationException` during query translation.
- Messages include provider-specific details for the unsupported shape (for example unsupported
  aggregates, joins, set operations, and offset-style operators).
- Translation failures occur before DynamoDB requests are executed.

## Model validation errors (startup)
- Shared-table key mappings fail model validation when entity types mapped to the same table disagree on key shape (PK-only vs PK+SK).
- Shared-table key mappings fail model validation when PK/SK attribute names differ across mapped entity types.
- Shared-table key mappings fail model validation when key type categories differ for the same PK/SK attribute (string vs number vs binary).
- Key properties fail model validation when they are nullable or resolve to nullable converter provider types.
- Key properties fail model validation when provider types are not DynamoDB key-compatible (`string`, number, `byte[]`), including `bool`.
- Root entity types fail model validation when they configure `HasKey(...)` or `[Key]` directly instead of using DynamoDB key mapping.
- Local secondary indexes fail model validation when the table does not resolve a DynamoDB sort key.
- Shared-table discriminator mappings fail model validation when discriminator values are duplicated.
- Shared-table discriminator mappings fail model validation when discriminator attribute names differ across entity types mapped to the same table.
- Shared-table discriminator mappings fail model validation when discriminator attribute name collides with PK or SK attribute names.

## Key mapping troubleshooting
- **Explicit root `HasKey(...)`**: root DynamoDB entities must use `HasPartitionKey(...)` and, when needed, `HasSortKey(...)`. The provider derives the EF primary key automatically.
- **No resolved Dynamo key**: if startup still fails after removing `HasKey(...)`, ensure the model resolves a DynamoDB partition key and optional sort key through explicit configuration or the supported naming conventions (`PK` / `PartitionKey`, `SK` / `SortKey`).
- **Local secondary index requires a sort key**: `HasLocalSecondaryIndex(...)` only works when the table already resolves both a DynamoDB partition key and sort key.

## Discriminator troubleshooting
- **Missing discriminator in results**: if returned items omit the configured discriminator attribute
  (default `$type`), materialization throws. Ensure stored items include the discriminator attribute.
- **Unknown discriminator value**: if returned discriminator value does not map to a concrete type in
  the model, materialization throws. Ensure discriminator values match configured model values.
- **Duplicate values in shared table**: assign unique discriminator values per entity type in a
  shared table group.
- **Name collision with keys**: do not reuse the discriminator attribute name for PK/SK attributes;
  change key attribute name overrides or discriminator name (`HasEmbeddedDiscriminatorName`).
- **Inheritance queries**: querying a base type can materialize derived CLR types. If derived
  properties are required/non-nullable and missing from stored items, materialization throws.

## How to interpret pagination logs
- Request log entries show configured `Limit` and whether a continuation token is present.
- Response log entries show returned item count and whether continuation is still required.
- Multiple requests usually indicate selective filters, a small page size, or DynamoDB page caps.

## Recommended practice
- Enable command logging in development and tests to verify translation behavior.

```csharp
services.AddLogging(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Information);
});
```

## What works today
- Query execution emits structured command diagnostics around each `ExecuteStatement` call.
- Pagination-related warning helps catch unbounded first-page scans on row-limiting queries.

## External references
- AWS ExecuteStatement API: <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_ExecuteStatement.html>
