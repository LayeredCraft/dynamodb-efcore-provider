using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public class MutablePocoSelectTests(DynamoContainerFixture fixture)
    : PrimitiveCollectionsTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ToListAsync_MaterializesMutablePocoWithDictionaryAndHashSetCollections()
    {
        var resultItems = await Db.MutableItems.ToListAsync(CancellationToken);

        resultItems.Should().BeEquivalentTo(PrimitiveCollectionsItems.MutableItems);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleAsync_MaterializesMutablePocoByPrimaryKey()
    {
        var pk = "ITEM#MUTABLE#A";

        var resultItem =
            await Db.MutableItems.SingleAsync(item => item.Pk == pk, CancellationToken);

        resultItem.Should().BeEquivalentTo(PrimitiveCollectionsItems.MutableItems[0]);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ToListAsync_MaterializesMutablePocoWithNullableCollectionElements()
    {
        var pk = "ITEM#MUTABLE#A";

        var resultItem =
            await Db.MutableItems.SingleAsync(item => item.Pk == pk, CancellationToken);

        resultItem.OptionalScores.Should().BeEquivalentTo(new List<int?> { 7, null, 9 });
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_MutatingCollectionsOnTrackedMutablePoco_PersistsAndReloads()
    {
        var pk = "ITEM#MUTABLE#A";

        var tracked = await Db.MutableItems.SingleAsync(item => item.Pk == pk, CancellationToken);
        tracked.ChargesByTier["bronze"] = 0.25m;
        tracked.RatingSet.Add(4);
        tracked.Tags.Add("gamma");
        await Db.SaveChangesAsync(CancellationToken);

        Db.ChangeTracker.Clear();

        var reloaded = await Db.MutableItems.SingleAsync(item => item.Pk == pk, CancellationToken);

        reloaded.ChargesByTier.Should().ContainKey("bronze").WhoseValue.Should().Be(0.25m);
        reloaded.RatingSet.Should().Contain(4);
        reloaded.Tags.Should().Contain("gamma");

        reloaded.ChargesByTier.Remove("bronze");
        reloaded.RatingSet.Remove(4);
        reloaded.Tags.Remove("gamma");
        await Db.SaveChangesAsync(CancellationToken);
    }
}
