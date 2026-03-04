using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

/// <summary>
///     Integration tests verifying that nested owned navigation property paths and list index
///     accesses are correctly translated to PartiQL and executed against DynamoDB.
/// </summary>
public class NestedPathWhereTests(OwnedTypesTableDynamoFixture fixture)
    : OwnedTypesTableTestBase(fixture)
{
    [Fact]
    public async Task Where_NestedEfPropertyChain_ReturnsMatchingItems()
    {
        var results =
            await Db
                .Items
                .Where(x => EF.Property<string>(
                        EF.Property<Profile?>(x, nameof(OwnedShapeItem.Profile))!,
                        nameof(Profile.DisplayName))
                    == "Ada")
                .ToListAsync(CancellationToken);

        var expected = OwnedTypesItems.Items.Where(x => x.Profile?.DisplayName == "Ada").ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "CreatedAt", "GuidValue", "IntValue", "Ratings", "StringValue", "Tags", "Orders", "OrderSnapshots", "Profile"
            FROM "OwnedTypesItems"
            WHERE "Profile"."DisplayName" = 'Ada'
            """);
    }

    [Fact]
    public async Task Where_SingleLevelOwnedProperty_ReturnsMatchingItems()
    {
        var results =
            await Db
                .Items
                .Where(x => x.Profile!.DisplayName == "Ada")
                .ToListAsync(CancellationToken);

        var expected = OwnedTypesItems.Items.Where(x => x.Profile?.DisplayName == "Ada").ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "CreatedAt", "GuidValue", "IntValue", "Ratings", "StringValue", "Tags", "Orders", "OrderSnapshots", "Profile"
            FROM "OwnedTypesItems"
            WHERE "Profile"."DisplayName" = 'Ada'
            """);
    }

    [Fact]
    public async Task Where_TwoLevelOwnedProperty_ReturnsMatchingItems()
    {
        var results =
            await Db
                .Items
                .Where(x => x.Profile!.Address!.City == "Seattle")
                .ToListAsync(CancellationToken);

        var expected =
            OwnedTypesItems.Items.Where(x => x.Profile?.Address?.City == "Seattle").ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "CreatedAt", "GuidValue", "IntValue", "Ratings", "StringValue", "Tags", "Orders", "OrderSnapshots", "Profile"
            FROM "OwnedTypesItems"
            WHERE "Profile"."Address"."City" = 'Seattle'
            """);
    }

    [Fact]
    public async Task Where_ThreeLevelOwnedProperty_ReturnsMatchingItems()
    {
        var results =
            await Db
                .Items
                .Where(x => x.Profile!.Address!.Geo!.Latitude > 0)
                .ToListAsync(CancellationToken);

        var expected =
            OwnedTypesItems.Items.Where(x => x.Profile?.Address?.Geo?.Latitude > 0).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "CreatedAt", "GuidValue", "IntValue", "Ratings", "StringValue", "Tags", "Orders", "OrderSnapshots", "Profile"
            FROM "OwnedTypesItems"
            WHERE "Profile"."Address"."Geo"."Latitude" > 0
            """);
    }

    [Fact]
    public async Task Where_TopLevelStringListIndex_ReturnsMatchingItems()
    {
        var results =
            await Db.Items.Where(x => x.Tags[0] == "featured").ToListAsync(CancellationToken);

        var expected =
            OwnedTypesItems.Items.Where(x => x.Tags.Count > 0 && x.Tags[0] == "featured").ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "CreatedAt", "GuidValue", "IntValue", "Ratings", "StringValue", "Tags", "Orders", "OrderSnapshots", "Profile"
            FROM "OwnedTypesItems"
            WHERE "Tags"[0] = 'featured'
            """);
    }

    [Fact]
    public async Task Where_TopLevelIntListIndex_ReturnsMatchingItems()
    {
        var results = await Db.Items.Where(x => x.Ratings[0] > 3).ToListAsync(CancellationToken);

        var expected =
            OwnedTypesItems.Items.Where(x => x.Ratings.Count > 0 && x.Ratings[0] > 3).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "CreatedAt", "GuidValue", "IntValue", "Ratings", "StringValue", "Tags", "Orders", "OrderSnapshots", "Profile"
            FROM "OwnedTypesItems"
            WHERE "Ratings"[0] > 3
            """);
    }

    [Fact]
    public async Task Where_NonZeroListIndex_ReturnsMatchingItems()
    {
        var results = await Db.Items.Where(x => x.Tags[1] == "vip").ToListAsync(CancellationToken);

        var expected =
            OwnedTypesItems.Items.Where(x => x.Tags.Count > 1 && x.Tags[1] == "vip").ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "CreatedAt", "GuidValue", "IntValue", "Ratings", "StringValue", "Tags", "Orders", "OrderSnapshots", "Profile"
            FROM "OwnedTypesItems"
            WHERE "Tags"[1] = 'vip'
            """);
    }
}
