# CLAUDE.md

This repo is an Entity Framework Core provider for AWS DynamoDB. It translates EF Core queries (
LINQ) into PartiQL and executes them via the AWS SDK.

## Where To Look First

- Provider code: `src/EntityFrameworkCore.DynamoDb/`
- Tests: `tests/EntityFrameworkCore.DynamoDb.Tests/`
- Query pipeline entry points (start here for query features):
  - LINQ translation:
    `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`
  - Query compilation:
    `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.cs`
  - PartiQL generation:
    `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
  - DynamoDB execution:
    `src/EntityFrameworkCore.DynamoDb/Storage/DynamoClientWrapper.cs`
  - Type mapping/conversion:
    `src/EntityFrameworkCore.DynamoDb/Storage/DynamoTypeMappingSource.cs`

## Making Changes (Practical Checklist)

- Start with a failing/added test in `tests/EntityFrameworkCore.DynamoDb.Tests/`.
- For new query translation behavior:
  - adjust translation in
    `src/EntityFrameworkCore.DynamoDb/Query/Internal/*TranslatingExpressionVisitor*.cs`
  - if needed, add a SQL expression under
    `src/EntityFrameworkCore.DynamoDb/Query/Internal/Expressions/`
  - emit PartiQL in
    `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
  - ensure type mapping (
    `src/EntityFrameworkCore.DynamoDb/Storage/DynamoTypeMappingSource.cs`) and
    materialization (
    `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoProjectionBindingRemovingExpressionVisitor.cs`)
    support the new shape
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
- Ensure all LINQ examples reflect currently supported translation (e.g., no method calls in `Where`
  unless supported).
- Where DynamoDB/PartiQL semantics matter, include an AWS reference link (ExecuteStatement, PartiQL
  SELECT/operators, AttributeValue).
- Docs site config is in `zensical.toml` (not `mkdocs.yml`); keep Zensical-related changes there.
- Verify docs build: `uv run zensical build` (or `task docs:build`).

## Internal EF Core APIs

This is a provider — use of EF Core internal APIs is explicitly permitted and expected. Suppress
`EF1001` at the file level (`#pragma warning disable EF1001`) in any file that needs them, and
leave a short comment explaining why (e.g. state manager access, `InternalEntityEntry` navigation).
Do not suppress warnings globally in the project file.

## Repo Hygiene

- Do not commit anything under `.claude/do_not_commit/`.
- Keep docs repo-relative (avoid machine-specific absolute paths).

## Code Style

- Prefer pattern matching over chained logical comparisons where possible.
- Prefer collection expressions over object initializers for collections.
- Always add comments to code to help explain complex logic or non-obvious behavior. This should
  provide context for future maintainers. Do not add simple comments that are obvious from the code.
- Always add XML comments when writing or modifying methods (both public and non-public):
  - Use `<summary>` for all methods - keep it to one or two sentences maximum
  - Add `<param>` and `<returns>` only when the purpose isn't obvious from naming
  - Focus on *why* or *what*, not *how* (code shows how)
  - Use `<remarks>` only when absolutely necessary for critical context that doesn't fit in summary
  - Example: `/// <summary>Translates LINQ Select expressions to PartiQL projections.</summary>`
  - Public facing methods should contain a full doc string that documents parameters and return
    value as well as any exceptions that may be thrown, and summary. Remarks may be included but
    should be concise.

<!-- BEGIN BEADS INTEGRATION v:1 profile:minimal hash:ca08a54f -->

## Beads Issue Tracker

This project uses **bd (beads)** for issue tracking. Run `bd prime` to see full workflow context and
commands.

### Quick Reference

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --claim  # Claim work
bd close <id>         # Complete work
```

### Rules

- Use `bd` for ALL task tracking — do NOT use TodoWrite, TaskCreate, or markdown TODO lists
- Run `bd prime` for detailed command reference and session close protocol
- Use `bd remember` for persistent knowledge — do NOT use MEMORY.md files

## Session Completion

**When ending a work session**, you MUST complete ALL steps below. Work is NOT complete until
`git push` succeeds.

**MANDATORY WORKFLOW:**

1. **File issues for remaining work** - Create issues for anything that needs follow-up
1. **Run quality gates** (if code changed) - Tests, linters, builds
1. **Update issue status** - Close finished work, update in-progress items
1. **PUSH TO REMOTE** - This is MANDATORY:
    ```bash
    git pull --rebase
    bd dolt push
    git push
    git status  # MUST show "up to date with origin"
    ```
1. **Clean up** - Clear stashes, prune remote branches
1. **Verify** - All changes committed AND pushed
1. **Hand off** - Provide context for next session

**CRITICAL RULES:**

- Work is NOT complete until `git push` succeeds
- NEVER stop before pushing - that leaves work stranded locally
- NEVER say "ready to push when you are" - YOU must push
- If push fails, resolve and retry until it succeeds

<!-- END BEADS INTEGRATION -->
