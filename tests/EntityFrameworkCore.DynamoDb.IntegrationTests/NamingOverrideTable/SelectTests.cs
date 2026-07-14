using EntityFrameworkCore.DynamoDb.IntegrationTests.NamingOverrideTable.Infra;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingOverrideTable;

public class SelectTests(DynamoContainerFixture fixture) : NamingOverridesTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ToListAsync_MaterializesNamingOverridesAndCollections()
    {
        var results = await Db.Items.ToListAsync(CancellationToken);

        var expected = NamingOverridesItems.Items.ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "sk", "$type", "bucketId", "bucketKey", "categoryId", "dateSubmitted", "game", "gs1-pk", "gs1-sk", "gs2-pk", "gs2-sk", "id", "message", "recordType", "tags", "answers"
            FROM "NamingOverridesItems"
            """);
    }
}
