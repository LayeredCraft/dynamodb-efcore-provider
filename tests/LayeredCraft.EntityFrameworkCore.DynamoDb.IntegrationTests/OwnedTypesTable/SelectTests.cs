using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

public class SelectTests(OwnedTypesTableDynamoFixture fixture) : OwnedTypesTableTestBase(fixture)
{
    [Fact]
    public async Task ToListAsync_MaterializesOwnedReferencesAndCollections()
    {
        var results = await Db.Items.ToListAsync(CancellationToken);

        var expected = OwnedTypesItems.Items.ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, IntValue, StringValue, GuidValue, CreatedAt, Tags, Ratings, Metadata, Profile, Orders, OrderSnapshots
            FROM OwnedTypesItems
            ORDER BY Pk ASC
            """);
    }

    [Fact]
    public async Task Select_NestedOwnedReferenceProjection_MaterializesShape()
    {
        var results =
            await Db.Items.Select(x => new { x.Pk, x.Profile }).ToListAsync(CancellationToken);

        var expected = OwnedTypesItems.Items.Select(x => new { x.Pk, x.Profile }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, Profile
            FROM OwnedTypesItems
            ORDER BY Pk ASC
            """);
    }

    [Fact]
    public async Task Select_NestedOwnedReferencePartialProjection_MaterializesShape()
    {
        var results =
            await Db.Items.Select(x => new { x.Pk, x.Profile.Age }).ToListAsync(CancellationToken);

        var expected = OwnedTypesItems.Items.Select(x => new { x.Pk, x.Profile.Age }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, Profile
            FROM OwnedTypesItems
            ORDER BY Pk ASC
            """);
    }

    [Fact]
    public async Task Select_NestedOwnedCollectionProjection_MaterializesShape()
    {
        var results =
            await Db
                .Items
                .Select(x => new { x.Pk, x.Orders, x.OrderSnapshots })
                .ToListAsync(CancellationToken);

        var expected =
            OwnedTypesItems
                .Items
                .OrderBy(x => x.Pk)
                .Select(x => new { x.Pk, x.Orders, x.OrderSnapshots })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, Orders, OrderSnapshots
            FROM OwnedTypesItems
            ORDER BY Pk ASC
            """);
    }
}
