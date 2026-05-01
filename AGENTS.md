# AGENTS.md

This repo is an EF Core provider for AWS DynamoDB: LINQ -> translation -> PartiQL -> AWS SDK
execution.

## Quick Map

- Provider code: `src/EntityFrameworkCore.DynamoDb/`
- Tests: `tests/EntityFrameworkCore.DynamoDb.Tests/`
- Query pipeline entry points:
  - Translation:
    `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`
  - Compilation:
    `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.cs`
  - PartiQL generation: `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
  - Execution: `src/EntityFrameworkCore.DynamoDb/Storage/DynamoClientWrapper.cs`
  - Type mapping: `src/EntityFrameworkCore.DynamoDb/Storage/DynamoTypeMappingSource.cs`

## Change Workflow

- Start with a failing or new test.
- For query behavior changes, update translation, add/adjust expressions if needed, update SQL
  generation, and verify type mapping/materialization.
- Keep changes small; cover both translation and execution behavior.

## Docs Requirement (Behavior Changes)

- Update `docs/operators.md` and relevant topical docs (`docs/pagination.md`, `docs/projections.md`,
  `docs/configuration.md`, `docs/diagnostics.md`, `docs/limitations.md`, `docs/architecture.md`).
- Keep docs user-facing; do not add internal code/test references.
- Ensure LINQ examples match current support.
- Add AWS references when DynamoDB/PartiQL semantics matter.
- Docs config is `zensical.toml`; verify with `uv run zensical build` (or `task docs:build`).

## EF Core Internals

- Internal EF Core API usage is expected.
- Suppress `EF1001` per-file only with `#pragma warning disable EF1001` and a short reason comment.
- Do not suppress warning globally.

## Repo Rules

- Keep docs paths repo-relative.
- When using the Beads workflow, see `BEADS.md` for command conventions and session protocol.

## Style Rules

- Use modern C# pattern matching where possible.
- Prefer collection expressions for collections.
- Add comments only for non-obvious logic.
- Add XML docs when writing or modifying all methods:
  - Always include `<summary>`.
  - Include `<param>`/`<returns>` where names are not self-explanatory.
  - Public methods should fully document parameters, returns, and thrown exceptions where relevant.
