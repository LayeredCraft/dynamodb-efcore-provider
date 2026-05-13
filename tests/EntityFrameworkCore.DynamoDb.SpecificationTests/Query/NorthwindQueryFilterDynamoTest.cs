using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.SpecificationTests.SharedInfra;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

public sealed class NorthwindQueryFilterDynamoTest(DynamoContainerFixture containerFixture)
    : IAsyncLifetime
{
    private readonly NorthwindQueryFilterFixture _fixture = new(containerFixture);

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Query_filter_is_applied_to_actual_and_filtered_expected_data()
        => await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Customer>(),
            ss => ss.Set<Customer>().Where(c => c.CompanyName.StartsWith("B")),
            elementSorter: c => c.CustomerID);

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task IgnoreQueryFilters_returns_unfiltered_data()
        => await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Customer>().IgnoreQueryFilters(),
            ss => ss.Set<Customer>(),
            elementSorter: c => c.CustomerID);

    private sealed class NorthwindQueryFilterFixture(DynamoContainerFixture containerFixture)
        : QueryFixtureBase<FilteredNorthwindDynamoContext>, IAsyncLifetime
    {
        private DynamoTestStore? _testStore;

        public QueryAsserter AssertQuery => new(this);

        public DynamoTestStore TestStore
            => _testStore
                ?? throw new InvalidOperationException("Fixture has not been initialized.");

        public override ISetSource ExpectedData => NorthwindData.Instance;

        public override IReadOnlyDictionary<Type, Func<object, object?>> EntitySorters
            => NorthwindEntitySorters.Create();

        public override IReadOnlyDictionary<Type, Action<object, object>> EntityAsserters
            => NorthwindEntityAsserters.Create();

        public async ValueTask InitializeAsync()
        {
            _testStore = new DynamoNorthwindTestStoreFactory(containerFixture.Client).Create();
            await _testStore.InitializeNorthwindAsync();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public override FilteredNorthwindDynamoContext CreateContext()
        {
            var builder = new DbContextOptionsBuilder<FilteredNorthwindDynamoContext>();
            builder
                .UseDynamo(options => options.DynamoDbClient(TestStore.Client))
                .ConfigureWarnings(w
                    => w
                        .Ignore(DynamoEventId.ScanLikeQueryDetected)
                        .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
            return new FilteredNorthwindDynamoContext(builder.Options);
        }
    }

    private sealed class FilteredNorthwindDynamoContext(DbContextOptions options)
        : NorthwindDynamoContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Customer>().HasQueryFilter(c => c.CompanyName.StartsWith("B"));
        }
    }
}
