using EntityFrameworkCore.DynamoDb.SpecificationTests.SharedInfra;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

public sealed class NorthwindFunctionsQueryDynamoTest(DynamoContainerFixture containerFixture)
    : IAsyncLifetime
{
    private readonly NorthwindQueryDynamoFixture<NoopModelCustomizer> _fixture =
        new(containerFixture);

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task String_startswith_translates_to_begins_with()
    {
        await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Customer>().Where(c => c.CompanyName.StartsWith("A")),
            elementSorter: c => c.CustomerID);

        _fixture.AssertPartiQl(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Northwind_Customers"
            WHERE begins_with("companyName", 'A')
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task String_contains_translates_to_contains()
    {
        await _fixture.AssertQuery.AssertQuery(
            ss => ss.Set<Customer>().Where(c => c.CompanyName.Contains("ar")),
            elementSorter: c => c.CustomerID);

        _fixture.AssertPartiQl(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Northwind_Customers"
            WHERE contains("companyName", 'ar')
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task String_endswith_is_explicitly_unsupported()
    {
        await using var context = _fixture.CreateContext();
        var act = async ()
            => await context.Set<Customer>().Where(c => c.CompanyName.EndsWith("s")).ToListAsync();
        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*could not be translated*");
    }
}
