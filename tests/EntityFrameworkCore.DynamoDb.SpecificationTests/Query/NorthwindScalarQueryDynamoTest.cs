using EntityFrameworkCore.DynamoDb.SpecificationTests.SharedInfra;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

public sealed class NorthwindScalarQueryDynamoTest(DynamoContainerFixture containerFixture)
    : IAsyncLifetime
{
    private readonly NorthwindQueryDynamoFixture<NoopModelCustomizer> _fixture =
        new(containerFixture);

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FirstOrDefault_entity_matches_expected_data()
        => await _fixture.AssertQuery.AssertSingleResult(
            ss => ss.Set<Customer>().Where(c => c.CustomerID == "ALFKI"),
            ss => ss.Set<Customer>().Where(c => c.CustomerID == "ALFKI"),
            q => q.FirstOrDefaultAsync(),
            q => q.FirstOrDefault(),
            (e, a) => a!.Should().BeEquivalentTo(e!, o => o.Excluding(ctx => ctx.Orders)));

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Count_is_explicitly_unsupported()
        => await _fixture.AssertQuery.AssertUnsupportedScalar(
            ss => ss.Set<Customer>().Where(c => c.City == "London"),
            "*Count*not supported*");
}
