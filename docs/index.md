---
icon: lucide/house
---

# EntityFrameworkCore.DynamoDb

Entity Framework Core provider for AWS DynamoDB.

This provider translates LINQ queries to PartiQL and executes them with the AWS SDK.

!!! warning

    This project is still under active development and is not production-ready yet.

## Install

```bash
dotnet add package EntityFrameworkCore.DynamoDb
```

## Quick Start

1. Configure the provider in your `DbContext` via `UseDynamo(...)`; see [Configuration](configuration.md).
1. Map your entities to DynamoDB tables and key schema; see [Indexes](indexes.md).
1. Start with supported LINQ operators from [Operators](operators.md).
1. Review [Limitations](limitations.md) before adopting query patterns.

## Current Scope

- Async query execution is supported.
- `SaveChangesAsync` is implemented for Added/Modified/Deleted root entities.
- Synchronous `SaveChanges` is not supported (DynamoDB API is async-only).
- LINQ translation support is partial; [Operators](operators.md) is the source of truth.
- Includes support for table mapping, key mapping, owned types, and secondary-index metadata.

## Compatibility

- .NET target framework: `net10.0`
- EF Core version: `10.0.x`
- AWS SDK dependency: `AWSSDK.DynamoDBv2` `4.x`
- Works with Amazon DynamoDB and DynamoDB Local.

## Issues and Help

- Report bugs and request features on [GitHub Issues](https://github.com/LayeredCraft/dynamodb-efcore-provider/issues).
- For local debugging guidance, use [Diagnostics](diagnostics.md).

## Documentation

- [Configuration](configuration.md)
- [Indexes](indexes.md)
- [Architecture](architecture.md)
- [Concurrency](concurrency.md)
- [Operators](operators.md)
- [Pagination](pagination.md)
- [Projections](projections.md)
- [Owned Types](owned-types.md)
- [Diagnostics](diagnostics.md)
- [Limitations](limitations.md)
- [Repository README](https://github.com/LayeredCraft/dynamodb-efcore-provider#readme)

## Notes

- `SaveChangesAsync` currently supports scalar root updates. Owned/nested mutation write paths are
    still limited; see [Limitations](limitations.md).
