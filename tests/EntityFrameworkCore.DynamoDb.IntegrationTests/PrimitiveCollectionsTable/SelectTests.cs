using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public class SelectTests(DynamoContainerFixture fixture)
    : PrimitiveCollectionsTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ToListAsync_MaterializesListMapSetProperties()
    {
        var resultItems = await Db.Items.ToListAsync(CancellationToken);

        resultItems.Should().BeEquivalentTo(PrimitiveCollectionsItems.Items);

        AssertSql(
            """
            SELECT "pk", "$type", "labelSet", "metadata", "optionalTags", "ratingSet", "scoresByCategory", "tags"
            FROM "PrimitiveCollectionsItems"
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Select_AnonymousProjection_WithCollectionProperties()
    {
        var results =
            await Db
                .Items
                .Select(item => new { item.Pk, item.Tags, item.LabelSet })
                .ToListAsync(CancellationToken);

        var expected =
            PrimitiveCollectionsItems
                .Items
                .Select(item => new { item.Pk, item.Tags, item.LabelSet })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "tags", "labelSet"
            FROM "PrimitiveCollectionsItems"
            """);
    }
}
