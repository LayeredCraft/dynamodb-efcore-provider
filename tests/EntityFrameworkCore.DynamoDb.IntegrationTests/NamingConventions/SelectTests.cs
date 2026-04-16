using EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventions.Infra;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventions;

public class SelectTests(DynamoContainerFixture fixture)
    : NamingConventionsTableTestFixture(fixture)
{
    [Fact]
    public async Task ToListAsync_MaterializesNamingConventionsAndCollections()
    {
        var results = await Db.Items.ToListAsync(CancellationToken);

        var expected = NamingConventionsItems.Items.ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "sk", "bucketId", "bucketKey", "categoryId", "dateSubmitted", "game", "gs1-pk", "gs1-sk", "gs2-pk", "gs2-sk", "id", "message", "recordType", "tags", "answers"
            FROM "NamingConventionsItems"
            """);
    }
}
