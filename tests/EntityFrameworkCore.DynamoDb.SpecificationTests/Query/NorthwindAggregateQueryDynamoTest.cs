using EntityFrameworkCore.DynamoDb.SpecificationTests.SharedInfra;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

public sealed class NorthwindAggregateQueryDynamoTest(DynamoContainerFixture containerFixture)
    : IAsyncLifetime
{
    private readonly NorthwindQueryDynamoFixture<NoopModelCustomizer> _fixture =
        new(containerFixture);

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Count_is_explicitly_unsupported()
        => await AssertUnsupportedAsync(q => q.CountAsync(), "*Count*not supported*");

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Any_is_explicitly_unsupported()
        => await AssertUnsupportedAsync(q => q.AnyAsync(), "*Any*not supported*");

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Sum_is_explicitly_unsupported()
        => await AssertUnsupportedAsync(
            q => q.SumAsync(c => c.CustomerID.Length),
            "*Sum*not supported*");

    private async Task AssertUnsupportedAsync(
        Func<IQueryable<Customer>, Task> operation,
        string message)
    {
        await using var context = _fixture.CreateContext();
        var query = context.Set<Customer>().Where(c => c.City == "London");
        var act = async () => await operation(query);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(message);
    }
}
