# Specification Tests

EF Core ships a cross-provider test suite in `Microsoft.EntityFrameworkCore.Specification.Tests`
(source: `efcore/test/EFCore.Specification.Tests/`). Each `*TestBase<TFixture>` class defines the
full test surface for a feature area. This project provides DynamoDB fixtures and overrides so
those tests run against a real DynamoDB Local instance.

## Shared Infrastructure

| File                                                   | Role                                                                                                                         |
|--------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------|
| `TestUtilities/DynamoSpecificationContainerFixture.cs` | Shared DynamoDB Local container (Testcontainers). Started asynchronously by spec test infrastructure and disposed after use. |
| `TestUtilities/DynamoTestStoreFactory.cs`              | Singleton factory. Plugs into EF Core test infra; vends `DynamoTestStore` and `TestSqlLoggerFactory`.                        |
| `TestUtilities/DynamoTestStore.cs`                     | Per-test store backed by the shared container client.                                                                        |
| `TestUtilities/TestSqlLoggerFactory.cs`                | Captures emitted PartiQL via `DynamoEventId.ExecutingPartiQlQuery` for baseline assertions.                                  |
| `TestUtilities/DynamoTestHelpers.cs`                   | `NoSyncTest()` helper — catches expected sync-query failures.                                                                |
| `TestUtilities/DynamoSpecificationFixture.cs`          | `IDynamoSpecificationFixture` interface + `ClearSql`/`AssertSql`/`ShouldLogDynamoSql` extensions shared by all fixtures.     |
| `AssemblyFixtures.cs`                                  | Disables test parallelization assembly-wide.                                                                                 |
| `DynamoSpecificationCollection`                        | xUnit collection fixture used by concrete spec test classes that need DynamoDB Local.                                        |

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

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(XxxDynamoTest));

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
    [Collection(DynamoSpecificationCollection.Name)]
    public class XxxDynamoTestDefault : XxxDynamoTest
    {
        public XxxDynamoTestDefault(
            XxxDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture)
            : base(fixture)
            => _ = containerFixture;
    }
}
```

### 3. Handle overrides

Every specification test class must include `Check_all_tests_overridden` and call
`DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(CurrentDynamoTestBase))`. Use the provider
spec-test base type, not `GetType()`, because concrete nested xUnit classes inherit from the
abstract provider base class.

Explicitly override every inherited spec test method. Each override must run the base test, skip
with a provider limitation reason, expect provider-specific failure, or assert provider-specific
behavior.

**Sync methods** — DynamoDB has no sync query path:

```csharp
public override void Some_sync_test()
    => NoSyncTest(() => base.Some_sync_test());
```

**Unsupported scenarios** — keep override wired to base implementation, even when skipped. Do not
use empty bodies or `Task.CompletedTask`; calling base preserves future compatibility if skip
removed or condition changes:

```csharp
[ConditionalFact(Skip = "DynamoDB does not support composite keys.")]
public override void Some_unsupported_test()
    => base.Some_unsupported_test();

[ConditionalTheory(Skip = "DynamoDB does not support composite keys.")]
public override Task Some_unsupported_test_async(CancellationType ct)
    => base.Some_unsupported_test_async(ct);
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
