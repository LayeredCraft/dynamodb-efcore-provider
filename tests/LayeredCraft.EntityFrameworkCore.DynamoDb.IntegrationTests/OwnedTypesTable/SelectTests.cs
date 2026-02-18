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
            SELECT Pk, CreatedAt, GuidValue, IntValue, Ratings, StringValue, Tags, Orders, OrderSnapshots, Profile
            FROM OwnedTypesItems
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
            """);
    }

    [Fact]
    public async Task Select_NestedOwnedReferencePartialProjection_MaterializesShape()
    {
        var results =
            await Db
                .Items
                .Select(x => new { x.Pk, Age = x.Profile == null ? null : x.Profile.Age })
                .ToListAsync(CancellationToken);

        var expected = OwnedTypesItems.Items.Select(x => new { x.Pk, x.Profile?.Age }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, Profile
            FROM OwnedTypesItems
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
            """);
    }

    [Fact]
    public async Task OwnedCollectionElements_HaveOrdinalKeys()
    {
        var item =
            await Db
                .Items
                .Where(x => x.Pk == "OWNED#3")
                .AsAsyncEnumerable()
                .SingleAsync(CancellationToken);

        Db.ChangeTracker.QueryTrackingBehavior.Should().Be(QueryTrackingBehavior.TrackAll);
        Db.Entry(item).State.Should().NotBe(EntityState.Detached);

        item.Orders.Should().NotBeNull();
        item.Orders.Should().NotBeEmpty();

        for (var i = 0; i < item.Orders.Count; i++)
        {
            var orderEntry = Db.Entry(item.Orders[i]);
            orderEntry.State.Should().NotBe(EntityState.Detached);
            var ordinalProperty =
                orderEntry.Metadata.GetProperties().Single(p => p.IsOwnedOrdinalKeyProperty());

            orderEntry.Metadata.FindPrimaryKey()!.Properties.Should().Contain(ordinalProperty);
            orderEntry.Property(ordinalProperty.Name).CurrentValue.Should().Be(i + 1);
        }

        AssertSql(
            """
            SELECT Pk, CreatedAt, GuidValue, IntValue, Ratings, StringValue, Tags, Orders, OrderSnapshots, Profile
            FROM OwnedTypesItems
            WHERE Pk = 'OWNED#3'
            """);
    }

    [Fact]
    public async Task Select_OwnedNavigationChain_IntermediateNull_PropagatesNull()
    {
        var results =
            await Db
                .Items
                .Select(x => new { x.Pk, x.Profile!.Address!.City })
                .ToListAsync(CancellationToken);

        results
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    new { Pk = "OWNED#1", City = (string?)"Seattle" },
                    new { Pk = "OWNED#2", City = (string?)null },
                    new { Pk = "OWNED#3", City = (string?)null },
                    new { Pk = "OWNED#4", City = (string?)null },
                });

        AssertSql(
            """
            SELECT Pk, Profile
            FROM OwnedTypesItems
            """);
    }

    [Fact]
    public async Task Select_OwnedNavigationChain_MissingAttribute_PropagatesNull()
    {
        var item = OwnedTypesItemMapper.ToItem(OwnedTypesItems.Items[0]);
        item["Pk"].S = "OWNED#MISSINGPROFILE";
        item.Remove("Profile");
        await PutItemAsync(item, CancellationToken);

        var result =
            await Db
                .Items
                .Where(x => x.Pk == "OWNED#MISSINGPROFILE")
                .Select(x => new { x.Pk, x.Profile!.Address!.City })
                .AsAsyncEnumerable()
                .SingleAsync(CancellationToken);

        result.Pk.Should().Be("OWNED#MISSINGPROFILE");
        result.City.Should().BeNull();

        AssertSql(
            """
            SELECT Pk, Profile
            FROM OwnedTypesItems
            WHERE Pk = 'OWNED#MISSINGPROFILE'
            """);
    }

    [Fact]
    public async Task
        ToListAsync_OwnedCollectionElement_WithOptionalOwnedReference_MixedNullMaterializes()
    {
        var item =
            await Db
                .Items
                .Where(x => x.Pk == "OWNED#3")
                .AsAsyncEnumerable()
                .SingleAsync(CancellationToken);

        var withoutPayment = item.Orders.Single(x => x.OrderNumber == "G-500");
        withoutPayment.Payment.Should().BeNull();

        var withPayment = item.Orders.Single(x => x.OrderNumber == "G-501");
        withPayment.Payment.Should().NotBeNull();
        withPayment.Payment!.Provider.Should().Be("stripe");
        withPayment.Payment.Card.Should().NotBeNull();
        withPayment.Payment.Card!.Last4.Should().Be("9999");

        AssertSql(
            """
            SELECT Pk, CreatedAt, GuidValue, IntValue, Ratings, StringValue, Tags, Orders, OrderSnapshots, Profile
            FROM OwnedTypesItems
            WHERE Pk = 'OWNED#3'
            """);
    }

    [Fact]
    public async Task NestedOwnedCollectionElements_HaveOrdinalKeys_ResetPerParent()
    {
        var item =
            await Db
                .Items
                .Where(x => x.Pk == "OWNED#3")
                .AsAsyncEnumerable()
                .SingleAsync(CancellationToken);

        foreach (var order in item.Orders)
            for (var i = 0; i < order.Lines.Count; i++)
            {
                var lineEntry = Db.Entry(order.Lines[i]);

                var ordinalProperty =
                    lineEntry.Metadata.GetProperties().Single(p => p.IsOwnedOrdinalKeyProperty());

                lineEntry.Metadata.FindPrimaryKey()!.Properties.Should().Contain(ordinalProperty);
                var id = lineEntry.Property(ordinalProperty.Name).CurrentValue;
                id.Should().Be(i + 1);
            }

        AssertSql(
            """
            SELECT Pk, CreatedAt, GuidValue, IntValue, Ratings, StringValue, Tags, Orders, OrderSnapshots, Profile
            FROM OwnedTypesItems
            WHERE Pk = 'OWNED#3'
            """);
    }

    [Fact]
    public async Task ToListAsync_AsNoTracking_OwnedCollections_MaterializeCorrectly()
    {
        var item =
            await Db
                .Items
                .AsNoTracking()
                .Where(x => x.Pk == "OWNED#3")
                .AsAsyncEnumerable()
                .SingleAsync(CancellationToken);

        item.Orders.Should().HaveCount(2);
        item.Orders.Single(x => x.OrderNumber == "G-500").Lines.Should().HaveCount(1);
        item.Orders.Single(x => x.OrderNumber == "G-501").Lines.Should().HaveCount(2);

        AssertSql(
            """
            SELECT Pk, CreatedAt, GuidValue, IntValue, Ratings, StringValue, Tags, Orders, OrderSnapshots, Profile
            FROM OwnedTypesItems
            WHERE Pk = 'OWNED#3'
            """);
    }
}
