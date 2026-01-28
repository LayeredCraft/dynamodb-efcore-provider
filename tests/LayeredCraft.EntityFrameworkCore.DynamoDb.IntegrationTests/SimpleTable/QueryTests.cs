using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class QueryTests(SimpleTableDynamoFixture fixture) : IClassFixture<SimpleTableDynamoFixture>
{
    private readonly SimpleTableDbContext _dbContext =
        SimpleTableDbContext.Create(fixture.Container.GetConnectionString());

    [Fact]
    public async Task ToListAsync_ReturnsAllItems()
    {
        var items = await _dbContext.SimpleItems.ToListAsync(TestContext.Current.CancellationToken);
        items.Should().NotBeEmpty();
    }
}
