using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class ModifiedScalarSaveChangesTests(DynamoContainerFixture fixture)
    : SimpleTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_ModifiedDateOnly_WritesCorrectWireFormat()
    {
        var item = await Db
            .SimpleItems
            .Where(x => x.Pk == "ITEM#2")
            .AsAsyncEnumerable()
            .SingleAsync(CancellationToken);

        SqlCapture.Clear();

        item.DateOnlyValue = new DateOnly(2025, 12, 25);

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
        // EF Core's DateOnlyToStringConverter writes "yyyy-MM-dd"
        rawItem.Item["dateOnlyValue"].S.Should().Be("2025-12-25");

        var actual = SimpleItemMapper.FromItem(rawItem.Item);
        actual.Should().BeEquivalentTo(item);

        AssertSql(
            """
            UPDATE "SimpleItems"
            SET "dateOnlyValue" = ?
            WHERE "pk" = ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_ModifiedTimeOnly_WritesCorrectWireFormat()
    {
        var item = await Db
            .SimpleItems
            .Where(x => x.Pk == "ITEM#2")
            .AsAsyncEnumerable()
            .SingleAsync(CancellationToken);

        SqlCapture.Clear();

        item.TimeOnlyValue = new TimeOnly(9, 15, 0);

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
        // EF Core's TimeOnlyToStringConverter writes "HH:mm:ss" for whole-second values
        rawItem.Item["timeOnlyValue"].S.Should().Be("09:15:00");

        var actual = SimpleItemMapper.FromItem(rawItem.Item);
        actual.Should().BeEquivalentTo(item);

        AssertSql(
            """
            UPDATE "SimpleItems"
            SET "timeOnlyValue" = ?
            WHERE "pk" = ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_ModifiedTimeSpan_WritesCorrectWireFormat()
    {
        var item = await Db
            .SimpleItems
            .Where(x => x.Pk == "ITEM#2")
            .AsAsyncEnumerable()
            .SingleAsync(CancellationToken);

        SqlCapture.Clear();

        item.TimeSpanValue = TimeSpan.FromHours(3);

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
        // EF Core's TimeSpanToStringConverter writes "c" format
        rawItem.Item["timeSpanValue"].S.Should().Be("03:00:00");

        var actual = SimpleItemMapper.FromItem(rawItem.Item);
        actual.Should().BeEquivalentTo(item);

        AssertSql(
            """
            UPDATE "SimpleItems"
            SET "timeSpanValue" = ?
            WHERE "pk" = ?
            """);
    }
}
