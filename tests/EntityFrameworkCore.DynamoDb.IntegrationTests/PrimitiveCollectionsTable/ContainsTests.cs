using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public class ContainsTests(DynamoContainerFixture fixture)
    : PrimitiveCollectionsTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_ListContains_WithCapturedParameter_TranslatesToPartiQlContains()
    {
        var tag = "alpha";

        var resultItems = await Db
            .Items
            .Where(item => item.Tags.Contains(tag))
            .ToListAsync(CancellationToken);

        var expected = PrimitiveCollectionsItems.Items.Where(item => item.Tags.Contains(tag));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "$type", "labelSet", "metadata", "optionalTags", "ratingSet", "scoresByCategory", "tags"
            FROM "PrimitiveCollectionsItems"
            WHERE contains("tags", ?)
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_StringSetContains_WithCapturedParameter_TranslatesToPartiQlContains()
    {
        var label = "common";

        var resultItems = await Db
            .Items
            .Where(item => item.LabelSet.Contains(label))
            .ToListAsync(CancellationToken);

        var expected = PrimitiveCollectionsItems.Items.Where(item => item.LabelSet.Contains(label));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "$type", "labelSet", "metadata", "optionalTags", "ratingSet", "scoresByCategory", "tags"
            FROM "PrimitiveCollectionsItems"
            WHERE contains("labelSet", ?)
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_NumberSetContains_WithInlineConstant_TranslatesToPartiQlContains()
    {
        var resultItems = await Db
            .Items
            .Where(item => item.RatingSet.Contains(2))
            .ToListAsync(CancellationToken);

        var expected = PrimitiveCollectionsItems.Items.Where(item => item.RatingSet.Contains(2));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "$type", "labelSet", "metadata", "optionalTags", "ratingSet", "scoresByCategory", "tags"
            FROM "PrimitiveCollectionsItems"
            WHERE contains("ratingSet", 2)
            """);
    }
}
