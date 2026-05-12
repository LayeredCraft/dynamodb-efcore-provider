# Specification Tests

EF Core ships a cross-provider test suite in `Microsoft.EntityFrameworkCore.Specification.Tests`
(source: `efcore/test/EFCore.Specification.Tests/`). Each `*TestBase<TFixture>` class defines the
full test surface for a feature area. This project provides DynamoDB fixtures and overrides so
those tests run against a real DynamoDB Local instance.

## Shared Infrastructure

| File                                                   | Role                                                                                                                     |
|--------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------|
| `TestUtilities/DynamoSpecificationContainerFixture.cs` | Process-scoped DynamoDB Local container (Testcontainers). Lazily started once per process.                               |
| `TestUtilities/DynamoTestStoreFactory.cs`              | Singleton factory. Plugs into EF Core test infra; vends `DynamoTestStore` and `TestSqlLoggerFactory`.                    |
| `TestUtilities/DynamoTestStore.cs`                     | Per-test store backed by the shared container client.                                                                    |
| `TestUtilities/TestSqlLoggerFactory.cs`                | Captures emitted PartiQL via `DynamoEventId.ExecutingPartiQlQuery` for baseline assertions.                              |
| `TestUtilities/DynamoTestHelpers.cs`                   | `NoSyncTest()` helper — catches expected sync-query failures.                                                            |
| `TestUtilities/DynamoSpecificationFixture.cs`          | `IDynamoSpecificationFixture` interface + `ClearSql`/`AssertSql`/`ShouldLogDynamoSql` extensions shared by all fixtures. |
| `AssemblyFixtures.cs`                                  | Disables test parallelization assembly-wide.                                                                             |

## Adding a New Specification Test

### 1. Find the base class

Browse `efcore/test/EFCore.Specification.Tests/` for the relevant `*TestBase.cs`. It defines all
virtual test methods to override. It also contains an inner `*FixtureBase` abstract class (e.g.
`FindTestBase<TFixture>.FindFixtureBase`) — that is the parent your fixture must extend, not any
external class.

### 2. Create the test class and fixture

```csharp
public abstract class XxxDynamoTest : XxxTestBase<XxxDynamoTest.XxxDynamoFixture>
{
    protected XxxDynamoTest(XxxDynamoFixture fixture) : base(fixture) => fixture.ClearSql();

    // overrides go here ...

    public class XxxDynamoFixture : XxxFixtureBase, IDynamoSpecificationFixture
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder)
                .UseDynamo(o => o.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        protected override async Task CleanAsync(DbContext context)
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            // Set table name and partition key for each entity type.
        }

        protected override Task SeedAsync(PoolableDbContext context)
        {
            context.AddRange(/* ... */);
            return context.SaveChangesAsync();
        }
    }

    // xUnit only discovers non-abstract classes; this subclass is the actual test class.
    public class XxxDynamoTestDefault(XxxDynamoFixture fixture) : XxxDynamoTest(fixture);
}
```

### 3. Handle overrides

**Sync methods** — DynamoDB has no sync query path:

```csharp
public override void Some_sync_test()
    => NoSyncTest(() => base.Some_sync_test());
```

**Unsupported scenarios** — skip with an empty body:

```csharp
[ConditionalFact(Skip = "DynamoDB does not support composite keys.")]
public override void Some_unsupported_test() { }

[ConditionalTheory(Skip = "DynamoDB does not support composite keys.")]
public override Task Some_unsupported_test_async(CancellationType ct) => Task.CompletedTask;
```

**Assert emitted PartiQL** — call `AssertSql(...)` after the base call. Pass no arguments for
tests expected to produce no SQL (e.g. cache hits):

```csharp
public override async Task Some_async_test(CancellationType ct)
{
    await base.Some_async_test(ct);
    AssertSql("""
        SELECT ...
        FROM ...
        WHERE ...
        """);
}
```

## Known DynamoDB Limitations

| Feature        | Skip reason                                   |
|----------------|-----------------------------------------------|
| Composite keys | `"DynamoDB does not support composite keys."` |
| Nullable keys  | `"DynamoDB does not support nullable keys."`  |
| Shadow keys    | `"DynamoDB does not support shadow keys."`    |

Define skip-reason constants at the top of each test class.
