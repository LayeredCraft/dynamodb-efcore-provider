using EntityFrameworkCore.DynamoDb.SpecificationTests.SharedInfra;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

public sealed class NorthwindSelectQueryDynamoTest(DynamoContainerFixture containerFixture)
    : IAsyncLifetime
{
    private readonly NorthwindQueryDynamoFixture<NoopModelCustomizer> _fixture =
        new(containerFixture);

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Select_scalar_property()
    {
        await _fixture.AssertQuery.AssertQuery(
            ss => ss
                .Set<Customer>()
                .Where(c => c.City == "London")
                .Select(c => new StringProjection(c.CustomerID)),
            elementSorter: p => p.Value);

        _fixture.AssertPartiQl(
            """
            SELECT "customerID"
            FROM "Northwind_Customers"
            WHERE "city" = 'London'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Select_anonymous_object()
    {
        await _fixture.AssertQuery.AssertQuery(
            ss => ss
                .Set<Customer>()
                .Where(c => c.City == "London")
                .Select(c => new { c.CustomerID, c.City }),
            elementSorter: p => p.CustomerID);

        _fixture.AssertPartiQl(
            """
            SELECT "customerID", "city"
            FROM "Northwind_Customers"
            WHERE "city" = 'London'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Select_dto_member_init()
    {
        await _fixture.AssertQuery.AssertQuery(
            ss => ss
                .Set<Customer>()
                .Where(c => c.City == "London")
                .Select(c => new CustomerProjection { Id = c.CustomerID, City = c.City }),
            elementSorter: p => p.Id);

        _fixture.AssertPartiQl(
            """
            SELECT "customerID", "city"
            FROM "Northwind_Customers"
            WHERE "city" = 'London'
            """);
    }

    public sealed record StringProjection(string Value);

    public sealed class CustomerProjection
    {
        public string Id { get; set; } = null!;
        public string City { get; set; } = null!;
    }
}
