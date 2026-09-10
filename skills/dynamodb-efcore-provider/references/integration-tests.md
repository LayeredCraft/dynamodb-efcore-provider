# DynamoDB EF Core Integration Tests

Integration tests live in `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/` and run against
DynamoDB Local through Testcontainers. Read `tests/AGENTS.md` first.

## Choose an existing table scenario

Reuse the closest suite instead of creating a generic fixture:

- `SimpleTable/` for basic scalar/query behavior
- `PkSkTable/` for partition and sort key behavior
- `ComplexTypesTable/` or `PrimitiveCollectionsTable/` for nested data
- `SecondaryIndexTable/`, `SecondaryIndexProjectionTable/`, or `CompetingGsiTable/` for indexes
- `SaveChangesTable/` for writes and concurrency

Create a new scenario only when none of these table shapes can represent the behavior clearly.

## Fixture and test pattern

Each suite has an `Infra/TestFixture.cs` derived from
`SharedInfra/DynamoTestFixtureBase.cs`. The base provides the DynamoDB Local client, options setup,
the current test cancellation token, and `AssertSql(...)` for captured PartiQL.

```csharp
public class WhereTests(DynamoContainerFixture fixture) : SimpleTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_Returns_matching_items()
    {
        var results = await Db.SimpleItems
            .AsNoTracking()
            .Where(item => item.Pk == "ITEM#1")
            .ToListAsync(CancellationToken);

        results.Should().BeEquivalentTo(
            SimpleItems.Items.Where(item => item.Pk == "ITEM#1"));

        AssertSql(
            """
            SELECT ...
            FROM "SimpleItems"
            WHERE "pk" = 'ITEM#1'
            """);
    }
}
```

## Rules

- Derive expected results from the suite seed collection; do not hardcode expected entities.
- Make expected LINQ null-safe and guard list indexing.
- Prefer `AsNoTracking()` unless tracking is the behavior under test.
- Use async query APIs and pass `CancellationToken`.
- Assert the full emitted PartiQL. Captured parameters render as `?`.
- Run focused tests while developing, then both EF-version suites before completing a code change.
