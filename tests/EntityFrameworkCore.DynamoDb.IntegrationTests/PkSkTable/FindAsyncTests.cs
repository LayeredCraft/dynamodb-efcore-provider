using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

/// <summary>Integration tests for <c>FindAsync</c> on a composite primary-key table.</summary>
public class FindAsyncTests(DynamoContainerFixture fixture) : PkSkTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FindAsync_CompositeKey_ReturnsMatchingItem_AndSetsLimit1()
    {
        LoggerFactory.Clear();

        var result = await Db.Items.FindAsync(["P#1", "0002"], CancellationToken);

        result
            .Should()
            .BeEquivalentTo(PkSkItems.Items.Single(item => item.Pk == "P#1" && item.Sk == "0002"));

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" = ? AND "sk" = ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FindAsync_CompositeKey_ReturnsNullWhenMissing()
    {
        LoggerFactory.Clear();

        var result = await Db.Items.FindAsync(["P#missing", "0001"], CancellationToken);

        result.Should().BeNull();

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);
    }
}
