using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class ModifiedScalarSaveChangesTests(SimpleTableDynamoFixture fixture)
    : SimpleTableTestBase(fixture)
{
    [Fact]
    public async Task SaveChangesAsync_ModifiedScalar_UsesPartitionKeyOnlyWhere_WhenNoSortKey()
    {
        var item = await Db
            .SimpleItems
            .Where(x => x.Pk == "ITEM#1")
            .AsAsyncEnumerable()
            .SingleAsync(CancellationToken);

        LoggerFactory.Clear();

        item.IntValue = 777;

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var rawItem = await Client.GetItemAsync(
            new GetItemRequest
            {
                TableName = SimpleTableDynamoFixture.TableName,
                Key = new Dictionary<string, AttributeValue> { ["Pk"] = new() { S = item.Pk } },
            },
            CancellationToken);

        rawItem.Item.Should().NotBeNull();

        var actual = SimpleItemMapper.FromItem(rawItem.Item);
        actual.Should().BeEquivalentTo(item);

        AssertSql(
            """
            UPDATE "SimpleItems"
            SET "IntValue" = ?, "$version" = ?
            WHERE "Pk" = ? AND "$version" = ?
            """);
    }
}
