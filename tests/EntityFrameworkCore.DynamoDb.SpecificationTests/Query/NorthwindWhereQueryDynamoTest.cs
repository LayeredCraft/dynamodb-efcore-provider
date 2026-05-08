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
    {
        await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Customer>().Where(c => c.City == "London"),
            elementSorter: c => c.CustomerID);

        _fixture.AssertPartiQl(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Northwind_Customers"
            WHERE "city" = 'London'
            """);
    }

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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Query_harness_supports_sync_execution()
        => await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Customer>().Where(c => c.Country == "UK"),
            elementSorter: c => c.CustomerID,
            async: false);

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData(QueryTrackingBehaviorVariant.TrackAll)]
    [InlineData(QueryTrackingBehaviorVariant.NoTracking)]
    [InlineData(QueryTrackingBehaviorVariant.NoTrackingWithIdentityResolution)]
    public async Task Query_harness_supports_tracking_variants(
        QueryTrackingBehaviorVariant tracking)
        => await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Customer>().Where(c => c.City == "London"),
            elementSorter: c => c.CustomerID,
            tracking: tracking);
}
