using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>ExecuteUpdate integration tests for numeric self-reference and multiple setters.</summary>
public class ExecuteUpdateTests(DynamoContainerFixture fixture) : SimpleTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithMultipleSetters_UpdatesAllAttributes()
    {
        var affected = await Db
            .SimpleItems
            .Where(item => item.Pk == "ITEM#1")
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.StringValue, "updated")
                    .SetProperty(item => item.IntValue, 777),
                CancellationToken);
        affected.Should().Be(1);

        AssertSql(
            """
            UPDATE "SimpleItems"
            SET "stringValue" = ?, "intValue" = ?
            WHERE "pk" = 'ITEM#1'
            """);

        var expected = SimpleItems.Items.Single(item => item.Pk == "ITEM#1") with
        {
            StringValue = "updated", IntValue = 777
        };
        (await Db.SimpleItems.FindAsync(["ITEM#1"], CancellationToken))!
            .Should()
            .BeEquivalentTo(expected);

        // Tests share the class-seeded table with no per-test reset; restore the seeded values
        // so test execution order does not affect later assertions.
        await Db
            .SimpleItems
            .Where(item => item.Pk == "ITEM#1")
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.StringValue, "alpha")
                    .SetProperty(item => item.IntValue, 100),
                CancellationToken);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithAdditionSelfReference_IncrementsStoredNumber()
    {
        var affected = await Db
            .SimpleItems
            .Where(item => item.Pk == "ITEM#1")
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.IntValue, item => item.IntValue + 50),
                CancellationToken);

        affected.Should().Be(1);

        var rawItem = await Client.GetItemAsync(
            new()
            {
                TableName = "SimpleItems",
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = "ITEM#1".ToAttributeValue()
                }
            },
            CancellationToken);
        rawItem.Item["intValue"].N.Should().Be("150");

        AssertSql(
            """
            UPDATE "SimpleItems"
            SET "intValue" = "intValue" + 50
            WHERE "pk" = 'ITEM#1'
            """);

        await Db
            .SimpleItems
            .Where(item => item.Pk == "ITEM#1")
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.IntValue, 100),
                CancellationToken);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithSubtractionSelfReference_DecrementsStoredNumber()
    {
        var affected = await Db
            .SimpleItems
            .Where(item => item.Pk == "ITEM#2")
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.IntValue, item => item.IntValue - 25),
                CancellationToken);

        affected.Should().Be(1);

        var rawItem = await Client.GetItemAsync(
            new()
            {
                TableName = "SimpleItems",
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = "ITEM#2".ToAttributeValue()
                }
            },
            CancellationToken);
        rawItem.Item["intValue"].N.Should().Be("199975");

        AssertSql(
            """
            UPDATE "SimpleItems"
            SET "intValue" = "intValue" - 25
            WHERE "pk" = 'ITEM#2'
            """);

        await Db
            .SimpleItems
            .Where(item => item.Pk == "ITEM#2")
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.IntValue, 200000),
                CancellationToken);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithSelfReferenceFromCapturedValue_ParameterizesOperand()
    {
        var increment = 5;
        var affected = await Db
            .SimpleItems
            .Where(item => item.Pk == "ITEM#3")
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.IntValue,
                    item => item.IntValue + increment),
                CancellationToken);

        affected.Should().Be(1);

        var rawItem = await Client.GetItemAsync(
            new()
            {
                TableName = "SimpleItems",
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = "ITEM#3".ToAttributeValue()
                }
            },
            CancellationToken);
        rawItem.Item["intValue"].N.Should().Be("987659");

        AssertSql(
            """
            UPDATE "SimpleItems"
            SET "intValue" = "intValue" + ?
            WHERE "pk" = 'ITEM#3'
            """);

        await Db
            .SimpleItems
            .Where(item => item.Pk == "ITEM#3")
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.IntValue, 987654),
                CancellationToken);
    }
}
