using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class ModifiedScalarSaveChangesTests(DynamoContainerFixture fixture)
    : SimpleTableTestFixture(fixture)
{
    [Fact]
    public async Task SaveChangesAsync_ModifiedScalar_UsesPartitionKeyOnlyWhere_WhenNoSortKey()
    {
        var item = await Db
            .SimpleItems
            .Where(x => x.Pk == "ITEM#1")
            .AsAsyncEnumerable()
            .SingleAsync(CancellationToken);

        SqlCapture.Clear();

        item.IntValue = 777;

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var rawItem = await Client.GetItemAsync(
            new GetItemRequest
            {
                TableName = SimpleItemTable.TableName,
                Key = new Dictionary<string, AttributeValue> { ["pk"] = new() { S = item.Pk } },
            },
            CancellationToken);

        rawItem.Item.Should().NotBeNull();

        var actual = SimpleItemMapper.FromItem(rawItem.Item);
        actual.Should().BeEquivalentTo(item);

        AssertSql(
            """
            UPDATE "SimpleItems"
            SET "intValue" = ?
            WHERE "pk" = ?
            """);
    }
}
