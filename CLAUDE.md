# CLAUDE.md

This repo is an Entity Framework Core provider for AWS DynamoDB. It translates EF Core queries (LINQ) into PartiQL and executes them via the AWS SDK.

## Where To Look First
- Provider code: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/`
- Tests: `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.Tests/`
- Query pipeline entry points (start here for query features):
  - LINQ translation: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`
  - Query compilation: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.cs`
  - PartiQL generation: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
  - DynamoDB execution: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Storage/DynamoClientWrapper.cs`
  - Type mapping/conversion: `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Storage/DynamoTypeMappingSource.cs`

## Build / Test
- Build: `dotnet build`
- Test (all): `dotnet test`
- Focused test project:
  `dotnet test tests/LayeredCraft.EntityFrameworkCore.DynamoDb.Tests/LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.csproj`
- SDK/runtime settings: `global.json`, `Directory.Build.props`, `Directory.Packages.props`

## Making Changes (Practical Checklist)
- Start with a failing/added test in `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.Tests/`.
- For new query translation behavior:
  - adjust translation in `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/*TranslatingExpressionVisitor*.cs`
  - if needed, add a SQL expression under `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/Expressions/`
  - emit PartiQL in `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
  - ensure type mapping (`src/LayeredCraft.EntityFrameworkCore.DynamoDb/Storage/DynamoTypeMappingSource.cs`) and materialization (`src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoProjectionBindingRemovingExpressionVisitor.cs`) support the new shape
- Keep changes small and add tests for both translation and execution behavior.

## Repo Hygiene
- Do not commit anything under `.claude/do_not_commit/`.
- Keep docs repo-relative (avoid machine-specific absolute paths).

## Code Style
- Prefer pattern matching over chained logical comparisons where possible.