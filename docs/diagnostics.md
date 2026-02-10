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

## Tests that cover this
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/SimpleTable/PaginationTests.cs`
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/PkSkTable/FirstTests.cs`

## Implementation anchors
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Diagnostics/DynamoEventId.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Diagnostics/Internal/DynamoLoggerExtensions.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Storage/DynamoClientWrapper.cs`

## External references
- AWS ExecuteStatement API: <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_ExecuteStatement.html>
