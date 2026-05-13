using EntityFrameworkCore.DynamoDb.SpecificationTests.SharedInfra;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

public sealed class NorthwindUnsupportedQueryDynamoTest(DynamoContainerFixture containerFixture)
    : IAsyncLifetime
{
    private readonly NorthwindQueryDynamoFixture<NoopModelCustomizer> _fixture =
        new(containerFixture);

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Cross_table_join_is_explicitly_unsupported()
        => await AssertUnsupportedAsync(ss
            => ss
                .Set<Customer>()
                .Join(ss.Set<Order>(), c => c.CustomerID, o => o.CustomerID, (c, o) => c));

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Include_navigation_is_explicitly_unsupported()
        => await AssertUnsupportedAsync(ss => ss.Set<Customer>().Include(c => c.Orders));

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Navigation_collection_filter_is_explicitly_unsupported()
        => await AssertUnsupportedAsync(ss => ss.Set<Customer>().Where(c => c.Orders.Any()));

    private async Task AssertUnsupportedAsync<TResult>(Func<ISetSource, IQueryable<TResult>> query)
    {
        await using var context = _fixture.CreateContext();
        var act = async () => await query(new DefaultSetSource(context)).ToListAsync();
        var assertion = await act.Should().ThrowAsync<InvalidOperationException>();
        assertion
            .Which
            .Message
            .Should()
            .Match(m => m.Contains("could not be translated", StringComparison.Ordinal)
                || m.Contains("invalid inside an 'Include' operation", StringComparison.Ordinal));
    }
}
