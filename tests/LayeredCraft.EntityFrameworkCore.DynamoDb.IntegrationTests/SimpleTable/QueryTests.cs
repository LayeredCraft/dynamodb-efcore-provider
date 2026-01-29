using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class QueryTests(SimpleTableDynamoFixture fixture) : IClassFixture<SimpleTableDynamoFixture>
{
    private readonly SimpleTableDbContext _dbContext =
        SimpleTableDbContext.Create(fixture.Container.GetConnectionString());

    [Fact]
    public async Task ToListAsync_ReturnsAllItems()
    {
        var resultItems =
            await _dbContext.SimpleItems.ToListAsync(TestContext.Current.CancellationToken);
        var originalItems = SimpleItems.Items;
        var attributeValues = SimpleItems.AttributeValues;
        resultItems.Should().BeEquivalentTo(SimpleItems.Items);
    }
}
