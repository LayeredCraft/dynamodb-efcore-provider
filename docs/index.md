# LayeredCraft.EntityFrameworkCore.DynamoDb

Entity Framework Core provider for AWS DynamoDB.

This provider translates LINQ queries to PartiQL and executes them with the AWS SDK.

## Documentation

- [Configuration](configuration.md)
- [Architecture](architecture.md)
- [Operators](operators.md)
- [Pagination](pagination.md)
- [Projections](projections.md)
- [Diagnostics](diagnostics.md)
- [Limitations](limitations.md)
- [Repository README](../README.md)

## Current scope

- Current scope is query execution (`ExecuteStatement`) with async query APIs.
- `SaveChanges` is not implemented yet.

## Where to look first (internals)

- LINQ translation: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`
- Query compilation/materialization: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.cs`
- PartiQL generation: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
- DynamoDB execution: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Storage/DynamoClientWrapper.cs`
- Type mapping: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Storage/DynamoTypeMappingSource.cs`

## Docs update policy (for every story)

1. Add or update tests that prove behavior.
2. Update `operators.md` when operator behavior changes.
3. Update the relevant topical page (`configuration.md`, `pagination.md`, `projections.md`, `diagnostics.md`, or `limitations.md`).
4. Keep behavior statements linked to both tests and implementation anchors.
