using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

/// <summary>Integration tests for ExecuteUpdate behavior against DynamoDB Local.</summary>
public class ExecuteUpdateTests(DynamoContainerFixture fixture) : PkSkTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_ByFullKey_UpdatesItem()
    {
        var affected = await Db
            .Items
            .Where(item => item.Pk == "P#1" && item.Sk == "0001")
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.Category, "updated"),
                CancellationToken);

        affected.Should().Be(1);

        AssertSql(
            """
            UPDATE "PkSkItems"
            SET "category" = ?
            WHERE "pk" = 'P#1' AND "sk" = '0001'
            """);

        var expected =
            PkSkItems.Items.Single(item => item.Pk == "P#1" && item.Sk == "0001") with
            {
                Category = "updated"
            };
        (await Db.Items.FindAsync(["P#1", "0001"], CancellationToken))!
            .Should()
            .BeEquivalentTo(expected);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithExtraPredicate_UpdatesOnlyMatchingItem()
    {
        var affected = await Db
            .Items
            .Where(item => item.Pk == "P#1" && item.Sk == "0002" && item.IsTarget)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.Category, "reclassified"),
                CancellationToken);

        affected.Should().Be(1);

        (await GetRawItemAsync("P#1", "0002", CancellationToken))!["category"]
            .S
            .Should()
            .Be("reclassified");

        AssertSql(
            """
            UPDATE "PkSkItems"
            SET "category" = ?
            WHERE "pk" = 'P#1' AND "sk" = '0002' AND "isTarget" = TRUE
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithMissingItem_ReturnsZero()
    {
        var affected = await Db
            .Items
            .Where(item => item.Pk == "P#missing" && item.Sk == "0001")
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.Category, "updated"),
                CancellationToken);

        affected.Should().Be(0);

        AssertSql(
            """
            UPDATE "PkSkItems"
            SET "category" = ?
            WHERE "pk" = 'P#missing' AND "sk" = '0001'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithCapturedValue_ParameterizesValue()
    {
        var category = "captured";
        var affected = await Db
            .Items
            .Where(item => item.Pk == "P#1" && item.Sk == "0003")
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.Category, category),
                CancellationToken);

        affected.Should().Be(1);

        AssertSql(
            """
            UPDATE "PkSkItems"
            SET "category" = ?
            WHERE "pk" = 'P#1' AND "sk" = '0003'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithNullValue_SetsNullWireValue()
    {
        var affected = await Db
            .Items
            .Where(item => item.Pk == "P#1" && item.Sk == "0004")
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.Category, (string?)null),
                CancellationToken);

        affected.Should().Be(1);

        var rawItem = await GetRawItemAsync("P#1", "0004", CancellationToken);
        rawItem!["category"].NULL.Should().BeTrue();

        AssertSql(
            """
            UPDATE "PkSkItems"
            SET "category" = ?
            WHERE "pk" = 'P#1' AND "sk" = '0004'
            """);
    }
}
