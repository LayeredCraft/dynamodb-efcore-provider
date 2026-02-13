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
    }

    [Fact]
    public async Task Select_NestedOwnedReferenceProjection_MaterializesShape()
    {
        var results =
            await Db.Items.Select(x => new { x.Pk, x.Profile }).ToListAsync(CancellationToken);

        var expected = OwnedTypesItems.Items.Select(x => new { x.Pk, x.Profile }).ToList();

        results.Should().BeEquivalentTo(expected);
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
    }

    [Fact]
    public async Task OwnedCollectionElements_HaveOrdinalKeys()
    {
        var item =
            (await Db.Items.Where(x => x.Pk == "OWNED#3").ToListAsync(CancellationToken)).Single();

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
    }
}
