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

## Documentation (Required For Behavior Changes)
When a story changes query behavior, update the docs in the same story so information is not lost.

- Update the canonical operator reference: `docs/operators.md`.
- Update relevant topical pages (as applicable):
  - `docs/pagination.md` (page size vs result limit, tokens, warnings)
  - `docs/projections.md` (server-side vs client-side, AttributeValue types)
  - `docs/configuration.md` (options, access-pattern notes)
  - `docs/diagnostics.md` (what is logged/warned)
  - `docs/limitations.md` (explicitly call out unsupported shapes)
  - `docs/architecture.md` (end-to-end flow, especially if the pipeline changes)
- Keep docs user-facing and minimal; avoid internal test/code links in published docs.
- Ensure all LINQ examples reflect currently supported translation (e.g., no method calls in `Where` unless supported).
- Where DynamoDB/PartiQL semantics matter, include an AWS reference link (ExecuteStatement, PartiQL SELECT/operators, AttributeValue).
- Verify docs build: `uv run zensical build` (or `task docs:build`).

## Repo Hygiene
- Do not commit anything under `.claude/do_not_commit/`.
- Keep docs repo-relative (avoid machine-specific absolute paths).

## Code Style
- Prefer pattern matching over chained logical comparisons where possible.
