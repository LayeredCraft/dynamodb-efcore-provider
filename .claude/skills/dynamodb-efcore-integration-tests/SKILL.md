---
name: dynamodb-efcore-integration-tests
description: Write DynamoDB Local integration tests for this EF Core provider (fixtures, per-test resets, seed comparisons, PartiQL baselines).
---

# DynamoDB EF Core Integration Tests

Write end-to-end integration tests that run EF Core queries against DynamoDB Local and assert both results and generated PartiQL.

## When to use

Use this skill when you:

- add or change query translation/execution behavior and need end-to-end verification
- add a new table suite under `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/`
- need to debug/lock down generated PartiQL via `AssertSql(...)`

## Quick start

1. Pick an existing suite under `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/` (or create a new one).
2. In a test:
  - prefer `[Fact(Timeout = TestConfiguration.DefaultTimeout)]` over plain `[Fact]`
   - execute the query on `Db` using `CancellationToken`
   - compute `expected` from the suite seed data (null-safe)
   - assert results with FluentAssertions
   - assert the exact PartiQL with `AssertSql("""...""")`
3. Run the integration test project.

## Conventions to follow

- Always compare against in-memory seed collections; avoid hardcoded keys.
- Mirror EF null-propagation in expected queries (`?.`), and guard list indexing.
- For single-item queries, use `query.AsAsyncEnumerable().SingleAsync(CancellationToken)`.
- For xUnit tests, use `[Fact(Timeout = TestConfiguration.DefaultTimeout)]` unless there is a
  documented reason not to.
- Prefer `AsNoTracking()` unless the test is explicitly about tracking.
- PartiQL baselines must be the full statement; parameterized values appear as `?`.

## References

- `.claude/skills/dynamodb-efcore-integration-tests/references/integration-tests.md`
