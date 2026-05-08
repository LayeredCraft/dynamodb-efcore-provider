using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.SpecificationTests.Query;
using EntityFrameworkCore.DynamoDb.SpecificationTests.SharedInfra;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

public sealed class DynamoTestStore(IAmazonDynamoDB client)
{
    public IAmazonDynamoDB Client { get; } = client;

    public Task InitializeNorthwindAsync(CancellationToken cancellationToken = default)
        => NorthwindTables.RecreateAndSeedAsync(Client, cancellationToken);
}

public sealed class DynamoNorthwindTestStoreFactory(IAmazonDynamoDB client)
{
    public DynamoTestStore Create() => new(client);
}

public sealed class NoopModelCustomizer : IModelCustomizer
{
    public void Customize(ModelBuilder modelBuilder, DbContext context) { }
}

public class NorthwindQueryDynamoFixture<TModelCustomizer>
    : QueryFixtureBase<NorthwindDynamoContext>, IAsyncLifetime
    where TModelCustomizer : class, IModelCustomizer
{
    private readonly DynamoContainerFixture _containerFixture;
    private DynamoTestStore? _testStore;

    public NorthwindQueryDynamoFixture(DynamoContainerFixture containerFixture)
        => _containerFixture = containerFixture;

    public TestPartiQlLoggerFactory TestPartiQlLoggerFactory { get; } = new();

    public QueryAsserter AssertQuery => new(this);

    public TestPartiQlLoggerFactory SqlCapture => TestPartiQlLoggerFactory;

    public DynamoTestStore TestStore
        => _testStore ?? throw new InvalidOperationException("Fixture has not been initialized.");

    public override ISetSource ExpectedData => NorthwindData.Instance;

    public override IReadOnlyDictionary<Type, Func<object, object?>> EntitySorters
        => NorthwindEntitySorters.Create();

    public override IReadOnlyDictionary<Type, Action<object, object>> EntityAsserters
        => NorthwindEntityAsserters.Create();

    public async ValueTask InitializeAsync()
    {
        _testStore = new DynamoNorthwindTestStoreFactory(_containerFixture.Client).Create();
        await _testStore.InitializeNorthwindAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public override NorthwindDynamoContext CreateContext() => new(CreateOptions());

    public DbContextOptions<NorthwindDynamoContext> CreateOptions()
    {
        var builder = new DbContextOptionsBuilder<NorthwindDynamoContext>();
        AddOptions(builder);
        return builder.Options;
    }

    public DbContextOptionsBuilder AddOptions(
        DbContextOptionsBuilder<NorthwindDynamoContext> builder)
        => builder
            .UseDynamo(options => options.DynamoDbClient(TestStore.Client))
            .UseLoggerFactory(TestPartiQlLoggerFactory)
            .ConfigureWarnings(w => w.Ignore(DynamoEventId.ScanLikeQueryDetected));

    public void ClearLog() => TestPartiQlLoggerFactory.Clear();

    public void AssertSql(params string[] expected)
        => TestPartiQlLoggerFactory.AssertBaseline(expected);

    public void AssertPartiQl(params string[] expected) => AssertSql(expected);
}
