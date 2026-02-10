using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public class ChangeTrackingTests(PrimitiveCollectionsDynamoFixture fixture)
    : PrimitiveCollectionsTestBase(fixture)
{
    [Fact]
    public async Task MutatingListMarksPropertyModified()
    {
        var entity =
            await Db.Items.AsTracking().FirstAsync(x => x.Pk == "ITEM#A", CancellationToken);

        Db.Entry(entity).State.Should().Be(EntityState.Unchanged);

        entity.Tags.Add("new");
        Db.ChangeTracker.DetectChanges();

        Db.Entry(entity).Property(e => e.Tags).IsModified.Should().BeTrue();
    }

    [Fact]
    public async Task MutatingDictionaryMarksPropertyModified()
    {
        var entity =
            await Db.Items.AsTracking().FirstAsync(x => x.Pk == "ITEM#A", CancellationToken);

        Db.Entry(entity).State.Should().Be(EntityState.Unchanged);

        entity.ScoresByCategory["math"] = entity.ScoresByCategory["math"] + 1;
        Db.ChangeTracker.DetectChanges();

        Db.Entry(entity).Property(e => e.ScoresByCategory).IsModified.Should().BeTrue();
    }

    [Fact]
    public async Task MutatingSetMarksPropertyModified()
    {
        var entity =
            await Db.Items.AsTracking().FirstAsync(x => x.Pk == "ITEM#A", CancellationToken);

        Db.Entry(entity).State.Should().Be(EntityState.Unchanged);

        entity.LabelSet.Add("new");
        Db.ChangeTracker.DetectChanges();

        Db.Entry(entity).Property(e => e.LabelSet).IsModified.Should().BeTrue();
    }
}
