using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.PrimitiveCollectionsTable;

/// <summary>Represents the SelectTests type.</summary>
public class SelectTests(DynamoContainerFixture fixture)
    : PrimitiveCollectionsTableTestFixture(fixture)
{
    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task ToListAsync_MaterializesListMapSetProperties()
    {
        var resultItems = await Db.Items.ToListAsync(CancellationToken);

        resultItems.Should().BeEquivalentTo(PrimitiveCollectionsItems.Items);

        AssertSql(
            """
            SELECT "Pk", "LabelSet", "Metadata", "OptionalTags", "RatingSet", "ScoresByCategory", "Tags"
            FROM "PrimitiveCollectionsItems"
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Skip = "Collection projection rewriting for anonymous types is not implemented yet.")]
    /// <summary>Provides functionality for this member.</summary>
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
            SELECT "Pk", "Tags", "LabelSet"
            FROM "PrimitiveCollectionsItems"
            """);
    }
}
