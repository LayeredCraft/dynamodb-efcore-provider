# Architecture

## End-to-end flow
1. LINQ query is translated into provider expressions.
2. Provider builds a `SelectExpression` query model.
3. Provider generates PartiQL SQL text and positional parameters.
4. Provider executes PartiQL via DynamoDB `ExecuteStatement`.
5. Provider materializes rows from `Dictionary<string, AttributeValue>` into projection/entity results.

## Core pipeline entry points
- LINQ translation: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`
- Query compilation: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.cs`
- PartiQL generation: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
- Execution wrapper: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Storage/DynamoClientWrapper.cs`
- Type mapping: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Storage/DynamoTypeMappingSource.cs`

## Core semantics captured by this architecture
- Query execution is async-only.
- Paging uses DynamoDB `NextToken` continuation unless disabled.
- Result limit and page size are separate concepts.
- Materialization enforces strict required-property behavior for missing/null/wrong-typed data.

## Tests that exercise the pipeline
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/SimpleTable/WhereTests.cs`
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/SimpleTable/SelectTests.cs`
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/SimpleTable/PaginationTests.cs`
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/PkSkTable/FirstTests.cs`
