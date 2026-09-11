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

- Standard MSBuild `Configuration` (`Debug`/`Release`) and standard
  `<TargetFrameworks>net10.0;net11.0</TargetFrameworks>` multi-targeting — no custom `EF10`/`EF11`
  configuration names. `$(TargetFramework)` drives EF-line package selection in
  `Directory.Packages.props`.
- Prefer Taskfile wrappers so restore/build/test use the same framework:
  - Build EF10: `task build:ef10`
  - Build EF11: `task build:ef11`
  - Build both debug targets: `task build:all`
  - Build arbitrary config/framework: `task build CONFIG="Debug" FRAMEWORK="net11.0"`
  - Test arbitrary config/framework: `task test FRAMEWORK="net10.0"`
- If EF11 fails locally because installed SDK is older than CI, use `task build:ef11:ci-sdk`; it
  installs the current 11.0 SDK into `.dotnet/ef11` and builds with that SDK.
- Raw CLI equivalent — no override needed for ordinary restore/build/test (multi-targets both
  TFMs by default; filter with `--framework` for one):
  - `dotnet restore EntityFrameworkCore.DynamoDb.slnx`
  - `dotnet build EntityFrameworkCore.DynamoDb.slnx --configuration Release --framework net11.0 --no-restore`
  - `dotnet test <project-or-slnx> --configuration Release --framework net11.0 --no-build`
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

## NativeAOT Smoke Test

- The NativeAOT smoke app (`testapps/EntityFrameworkCore.DynamoDb.NativeAotSmoke`) and its CI leg
  (`.github/workflows/pr-build.yaml` `native-aot` job) are CI-validated for **both** EF10
  (`net10.0`) and EF11 (`net11.0`).
- NativeAOT publish/query precompilation requires the project to evaluate as genuinely
  single-targeted, which an externally supplied `-p:TargetFramework` cannot achieve reliably (EF's
  own NativeAOT tooling loses such external build context internally — see
  `docs/internal/ef10-ef11-build-configuration-strategy-research.md`). Generate the physical,
  gitignored override first: `scripts/write-target-framework-override.sh <net10.0|net11.0>`; clear
  it afterward with `scripts/write-target-framework-override.sh --clear`. `task test:aot-publish
  FRAMEWORK=net10.0`/`net11.0` does both automatically, including on failure.
- Full details: `testapps/EntityFrameworkCore.DynamoDb.NativeAotSmoke/AGENTS.md`. Read it before
  touching the smoke app or the `native-aot` CI job.
- `task test:aot-generation` and the full test suites run on both EF10 and EF11 without needing
  the override.

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
