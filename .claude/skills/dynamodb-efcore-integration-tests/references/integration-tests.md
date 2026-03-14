# DynamoDB EF Core Provider Integration Tests (Repo Conventions)

Integration tests live in `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/` and run against DynamoDB Local (Testcontainers).

## Project layout

- Test project: `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/EntityFrameworkCore.DynamoDb.IntegrationTests.csproj`
- Suites (examples):
  - `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/SimpleTable/`
  - `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/OwnedTypesTable/`
- Shared helpers:
  - `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/TestUtilities/DynamoDbPerTestResetTestBase.cs`
  - `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/TestUtilities/DynamoDbSchemaManager.cs`

## Base classes (how tests get Db, seeding, and AssertSql)

- `DynamoDbTestBase<TFixture, TContext>`
  - builds `DbContextOptions` via `UseDynamo(...)` with the fixture's `IAmazonDynamoDB`
  - exposes `CancellationToken` via `TestContext.Current.CancellationToken`
- `DynamoDbPerTestResetTestBase<TFixture, TContext>`
  - per test: deletes all tables, (re)creates tables, seeds, creates `Db` + logger
  - provides `AssertSql(params string[] expected)` to baseline captured PartiQL

## Canonical test structure

```csharp
[Fact]
public async Task Where_SomeCondition_ReturnsMatchingItems()
{
    var results = await Db.Items
        .AsNoTracking()
        .Where(x => x.SomeProperty == "value")
        .ToListAsync(CancellationToken);

    var expected = SeedDataClass.Items
        .Where(x => x.SomeProperty == "value") // keep expected null-safe when applicable
        .ToList();

    results.Should().BeEquivalentTo(expected);

    AssertSql(
        """
        SELECT ...
        FROM "TableName"
        WHERE "SomeProperty" = ?
        """);
}
```

Notes:

- PartiQL uses `?` placeholders for parameterized values in baselines.
- Always baseline the full statement (including SELECT list, FROM, WHERE, ORDER BY, LIMIT, etc.).

## Rules of thumb (keep these consistent across suites)

- Expected results:
  - Always derive from the in-memory seed data for that suite (e.g. `OwnedTypesItems.Items`).
  - Avoid hardcoding `Pk`/`Sk` values or building ad-hoc expected objects unless the test itself inserts the data.
- Null semantics:
  - Mirror EF Core null-propagation (`?.`) in your expected LINQ.
  - Guard list indexing (e.g. `x.Tags.Count > 0 && x.Tags[0] == ...`).
- Single item queries:
  - Prefer `await query.AsAsyncEnumerable().SingleAsync(CancellationToken)`.
- Tracking:
  - Prefer `AsNoTracking()` unless the test asserts tracking behavior.

## Creating a new suite (minimal checklist)

1. Add a suite folder under `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/`.
2. Create a fixture that starts DynamoDB Local:

```csharp
public sealed class MySuiteDynamoFixture : DynamoFixture
{
    public const string TableName = "MySuiteItems";
}
```

3. Create a suite base that derives from `DynamoDbPerTestResetTestBase<..., ...>`:

```csharp
public abstract class MySuiteTestBase
    : DynamoDbPerTestResetTestBase<MySuiteDynamoFixture, MySuiteDbContext>
{
    protected MySuiteTestBase(MySuiteDynamoFixture fixture) : base(fixture)
    {
    }

    protected override async Task CreateTablesAsync(CancellationToken cancellationToken)
    {
        // CreateTableAsync(...) then wait for ACTIVE.
        // Use DynamoDbSchemaManager.WaitForTableActiveAsync(...) where applicable.
    }

    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        // Insert seed items (BatchWriteItem is typically chunked in batches of 25;
        // retry UnprocessedItems until cleared).
    }
}
```

4. Add seed data for the suite (both typed objects and AttributeValue maps as needed).
5. Add tests that follow the canonical structure and assert PartiQL via `AssertSql`.

## Running integration tests

- Use the `dotnet-test-mcp` runner when available.
- Or run directly:

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.IntegrationTests/EntityFrameworkCore.DynamoDb.IntegrationTests.csproj
```
