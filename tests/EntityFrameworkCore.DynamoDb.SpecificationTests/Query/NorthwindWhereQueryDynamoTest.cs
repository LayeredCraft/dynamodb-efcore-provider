using EntityFrameworkCore.DynamoDb.SpecificationTests.SharedInfra;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

public sealed class NorthwindWhereQueryDynamoTest(DynamoContainerFixture containerFixture)
    : IAsyncLifetime
{
    private readonly NorthwindQueryDynamoFixture<NoopModelCustomizer> _fixture =
        new(containerFixture);

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_string_equality()
        => await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Customer>().Where(c => c.City == "London"),
            elementSorter: c => c.CustomerID);

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_numeric_greater_than()
        => await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Product>().Where(p => p.ProductID > 10),
            elementSorter: p => p.ProductID);

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_bool_equality()
        => await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Product>().Where(p => !p.Discontinued),
            elementSorter: p => p.ProductID);
}
