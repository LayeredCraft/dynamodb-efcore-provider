using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

public class ExecuteDeleteTests(DynamoContainerFixture fixture) : PkSkTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteDeleteAsync_ByFullKey_DeletesItem()
    {
        var item = PkSkItems.Items.Single(item => item.Pk == "P#1" && item.Sk == "0001");

        try
        {
            var affected =
                await Db
                    .Items
                    .Where(current => current.Pk == item.Pk && current.Sk == item.Sk)
                    .ExecuteDeleteAsync(CancellationToken);

            affected.Should().Be(1);
            (await GetRawItemAsync(item.Pk, item.Sk, CancellationToken)).Should().BeNull();

            AssertSql(
                """
                DELETE FROM "PkSkItems"
                WHERE "pk" = ? AND "sk" = ?
                """);
        }
        finally
        {
            Db.Items.Add(item);
            await Db.SaveChangesAsync(CancellationToken);
        }
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteDeleteAsync_WithNonMatchingPredicate_ReturnsZero()
    {
        var affected =
            await Db
                .Items
                .Where(item => item.Pk == "P#1" && item.Sk == "0002" && !item.IsTarget)
                .ExecuteDeleteAsync(CancellationToken);

        affected.Should().Be(0);
        (await GetRawItemAsync("P#1", "0002", CancellationToken)).Should().NotBeNull();

        AssertSql(
            """
            DELETE FROM "PkSkItems"
            WHERE "pk" = 'P#1' AND "sk" = '0002' AND NOT ("isTarget" = TRUE)
            """);
    }
}
