using EntityFrameworkCore.DynamoDb.SpecificationTests.SharedInfra;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

public sealed class NorthwindNullSemanticsQueryDynamoTest(DynamoContainerFixture containerFixture)
    : IAsyncLifetime
{
    private readonly NorthwindQueryDynamoFixture<NoopModelCustomizer> _fixture =
        new(containerFixture);

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Nullable_property_equals_null_matches_expected_data()
        => await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Product>().Where(p => p.UnitPrice == null),
            elementSorter: p => p.ProductID);

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Nullable_property_not_equals_null_matches_expected_data()
        => await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Product>().Where(p => p.UnitPrice != null),
            elementSorter: p => p.ProductID);
}
