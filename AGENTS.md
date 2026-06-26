# AGENTS.md

At session start, load `AGENTS.local.md` if it exists.

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

- This repo uses named EF build configurations, not plain `Debug`/`Release`:
  - `Debug EF10`, `Release EF10` -> `net10.0`, EF Core 10 packages
  - `Debug EF11`, `Release EF11` -> `net11.0`, EF Core 11 packages
- Prefer Taskfile wrappers so restore/build/test use the same configuration:
  - Build EF10: `task build:ef10`
  - Build EF11: `task build:ef11`
  - Build both debug targets: `task build:all`
  - Build arbitrary config: `task build CONFIG="Debug EF11"`
  - Test arbitrary config: `task test CONFIG="Debug EF10"`
  - Spec tests: `task test:spec CONFIG="Debug EF11"`
  - Pack releases: `task pack:ef10`, `task pack:ef11`
- If EF11 fails locally because installed SDK is older than CI, use `task build:ef11:ci-sdk`; it
  installs the current 11.0 SDK into `.dotnet/ef11` and builds with that SDK.
- Raw CLI equivalent must pass the configuration to restore and build/test:
  - `dotnet restore EntityFrameworkCore.DynamoDb.slnx -p:Configuration="Debug EF11"`
  - `dotnet build EntityFrameworkCore.DynamoDb.slnx --configuration "Debug EF11" --no-restore`
  - `dotnet test <project-or-slnx> --configuration "Debug EF11" --no-build`
- Manage NuGet packages with the `dotnet` CLI (`dotnet add package`, `dotnet package update`,
  `dotnet remove package`, etc.); do not hand-edit package references unless the CLI cannot express
  the needed change.
- Run tests through the .NET test MCP server whenever available.
- Before returning testing results for any code change, run both EF-version suites:
  `task test:ef10` and `task test:ef11`.
- Unit test project:
  `tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj`
- Integration test project:
  `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/EntityFrameworkCore.DynamoDb.IntegrationTests.csproj`
- Integration tests print generated PartiQL to standard output via `TestPartiQlLoggerFactory`.
  When debugging query failures, run with xUnit live output (for example `--show-live-output on`)
  or inspect `AssertSql` failure messages to see captured statements.
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
- Add XML docs only when they help API consumers or clarify non-obvious behavior.
  - Do not add XML docs to tests, test helpers, local functions, or obvious private/internal members
    unless useful.
  - Public API methods should include `<summary>` and document parameters, returns, and thrown
    exceptions where relevant.
