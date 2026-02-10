using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public class WhereTests(PrimitiveCollectionsDynamoFixture fixture)
    : PrimitiveCollectionsTestBase(fixture)
{
    [Fact(Skip = "Collection query translation not implemented yet.")]
    public async Task Where_ListContains_TranslatesToContainsFunction()
    {
        var resultItems =
            await Db
                .Items.Where(item => item.Tags.Contains("alpha"))
                .ToListAsync(CancellationToken);

        var expected = PrimitiveCollectionsItems.Items.Where(item => item.Tags.Contains("alpha"));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, Tags, ScoresByCategory, LabelSet, RatingSet, Metadata, OptionalTags
            FROM PrimitiveCollectionsItems
            WHERE contains(Tags, 'alpha')
            """);
    }

    [Fact(Skip = "Collection query translation not implemented yet.")]
    public async Task Where_NotListContains_TranslatesToNotContainsFunction()
    {
        var resultItems =
            await Db
                .Items.Where(item => !item.Tags.Contains("alpha"))
                .ToListAsync(CancellationToken);

        var expected = PrimitiveCollectionsItems.Items.Where(item => !item.Tags.Contains("alpha"));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, Tags, ScoresByCategory, LabelSet, RatingSet, Metadata, OptionalTags
            FROM PrimitiveCollectionsItems
            WHERE NOT contains(Tags, 'alpha')
            """);
    }

    [Fact(Skip = "Collection query translation not implemented yet.")]
    public async Task Where_SetContains_TranslatesToContainsFunction()
    {
        var resultItems =
            await Db
                .Items.Where(item => item.LabelSet.Contains("common"))
                .ToListAsync(CancellationToken);

        var expected =
            PrimitiveCollectionsItems.Items.Where(item => item.LabelSet.Contains("common"));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, Tags, ScoresByCategory, LabelSet, RatingSet, Metadata, OptionalTags
            FROM PrimitiveCollectionsItems
            WHERE contains(LabelSet, 'common')
            """);
    }

    [Fact(Skip = "Collection query translation not implemented yet.")]
    public async Task Where_NotSetContains_TranslatesToNotContainsFunction()
    {
        var resultItems =
            await Db
                .Items.Where(item => !item.LabelSet.Contains("common"))
                .ToListAsync(CancellationToken);

        var expected =
            PrimitiveCollectionsItems.Items.Where(item => !item.LabelSet.Contains("common"));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, Tags, ScoresByCategory, LabelSet, RatingSet, Metadata, OptionalTags
            FROM PrimitiveCollectionsItems
            WHERE NOT contains(LabelSet, 'common')
            """);
    }

    [Fact(Skip = "Collection query translation not implemented yet.")]
    public async Task Where_MapContainsKey_TranslatesToIsNotMissing()
    {
        var resultItems =
            await Db
                .Items.Where(item => item.ScoresByCategory.ContainsKey("math"))
                .ToListAsync(CancellationToken);

        var expected =
            PrimitiveCollectionsItems.Items.Where(item
                => item.ScoresByCategory.ContainsKey("math"));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, Tags, ScoresByCategory, LabelSet, RatingSet, Metadata, OptionalTags
            FROM PrimitiveCollectionsItems
            WHERE ScoresByCategory.math IS NOT MISSING
            """);
    }

    [Fact(Skip = "Collection query translation not implemented yet.")]
    public async Task Where_NotMapContainsKey_TranslatesToIsMissing()
    {
        var resultItems =
            await Db
                .Items.Where(item => !item.ScoresByCategory.ContainsKey("math"))
                .ToListAsync(CancellationToken);

        var expected =
            PrimitiveCollectionsItems.Items.Where(item
                => !item.ScoresByCategory.ContainsKey("math"));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, Tags, ScoresByCategory, LabelSet, RatingSet, Metadata, OptionalTags
            FROM PrimitiveCollectionsItems
            WHERE ScoresByCategory.math IS MISSING
            """);
    }
}
