using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public class SelectTests(PrimitiveCollectionsDynamoFixture fixture)
    : PrimitiveCollectionsTestBase(fixture)
{
    [Fact]
    public async Task ToListAsync_MaterializesListMapSetProperties()
    {
        var resultItems = await Db.Items.ToListAsync(CancellationToken);

        resultItems.Should().BeEquivalentTo(PrimitiveCollectionsItems.Items);

        AssertSql(
            """
            SELECT Pk, LabelSet, Metadata, OptionalTags, RatingSet, ScoresByCategory, Tags
            FROM PrimitiveCollectionsItems
            """);
    }

    [Fact(Skip = "Collection projection rewriting for anonymous types is not implemented yet.")]
    public async Task Select_AnonymousProjection_WithCollectionProperties()
    {
        var results =
            await Db
                .Items.Select(item => new { item.Pk, item.Tags, item.LabelSet })
                .ToListAsync(CancellationToken);

        var expected =
            PrimitiveCollectionsItems
                .Items.Select(item => new { item.Pk, item.Tags, item.LabelSet })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, Tags, LabelSet
            FROM PrimitiveCollectionsItems
            """);
    }
}
