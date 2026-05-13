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
    {
        await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Product>().Where(p => p.ProductID > 10),
            elementSorter: p => p.ProductID);

        _fixture.AssertPartiQl(
            """
            SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
            FROM "Northwind_Products"
            WHERE "productID" > 10
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_bool_equality()
    {
        await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Product>().Where(p => !p.Discontinued),
            elementSorter: p => p.ProductID);

        _fixture.AssertPartiQl(
            """
            SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
            FROM "Northwind_Products"
            WHERE NOT ("discontinued" = TRUE)
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_string_inequality()
    {
        await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Customer>().Where(c => c.City != "London"),
            elementSorter: c => c.CustomerID);

        _fixture.AssertPartiQl(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Northwind_Customers"
            WHERE "city" <> 'London'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_numeric_range_with_and()
    {
        await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Product>().Where(p => p.ProductID >= 10 && p.ProductID <= 20),
            elementSorter: p => p.ProductID);

        _fixture.AssertPartiQl(
            """
            SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
            FROM "Northwind_Products"
            WHERE "productID" BETWEEN 10 AND 20
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_string_or_predicate()
    {
        await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Customer>().Where(c => c.City == "London" || c.City == "Berlin"),
            elementSorter: c => c.CustomerID);

        _fixture.AssertPartiQl(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Northwind_Customers"
            WHERE "city" = 'London' OR "city" = 'Berlin'
            """);
    }

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
