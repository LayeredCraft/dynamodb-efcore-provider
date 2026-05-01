# AGENTS.md

This repo is an EF Core provider for AWS DynamoDB: LINQ -> translation -> PartiQL -> AWS SDK
execution.

## Quick Map

- Provider code: `src/EntityFrameworkCore.DynamoDb/`
- Unit tests: `tests/EntityFrameworkCore.DynamoDb.Tests/`
- Integration tests: `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/`
- Test placement rules: `tests/AGENTS.md`
- Translation:
  `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`
- Compilation:
  `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.cs`
- PartiQL generation: `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
- Execution: `src/EntityFrameworkCore.DynamoDb/Storage/DynamoClientWrapper.cs`
- Type mapping: `src/EntityFrameworkCore.DynamoDb/Storage/DynamoTypeMappingSource.cs`

## Commands

- Build: `dotnet build`
- Run tests through the .NET test MCP server whenever available.
- Unit test project:
  `tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj`
- Integration test project:
  `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/EntityFrameworkCore.DynamoDb.IntegrationTests.csproj`
- Docs: `task docs:build`

## Change Workflow

- Start with a failing or new test.
- For query behavior changes, update translation, expressions if needed, PartiQL generation, and
  materialization/type mapping.
- Keep changes small; cover both translation and execution behavior.

## Docs Requirement (Behavior Changes)

- Query behavior docs: `docs/querying/`
- Configuration/modeling docs: `docs/configuration/`, `docs/modeling/`
- Saving docs: `docs/saving/`
- Diagnostics/limits docs: `docs/diagnostics.md`, `docs/limitations.md`
- Keep docs user-facing; do not add internal code/test references.
- Ensure LINQ examples match current support.
- Add AWS references when DynamoDB/PartiQL semantics matter.
- Docs config is `zensical.toml`; verify with `uv run zensical build` (or `task docs:build`).

## Repo Rules

- Keep docs paths repo-relative.
- When using the Beads workflow, see `BEADS.md` for command conventions and session protocol.

## Style Rules

- Use modern C# pattern matching where possible.
- Prefer collection expressions for collections.
- Add comments only for non-obvious logic.
- Add XML docs for all methods:
  - Always include `<summary>`.
  - Include `<param>`/`<returns>` where names are not self-explanatory.
  - Public methods should fully document parameters, returns, and thrown exceptions where relevant.
