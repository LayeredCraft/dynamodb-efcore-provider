using System.Collections.ObjectModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public class OptimizationBehaviorTests(PrimitiveCollectionsDynamoFixture fixture)
    : PrimitiveCollectionsTestBase(fixture)
{
    [Fact]
    public void SetComparer_Snapshot_PreservesHashSetComparer()
    {
        var property =
            Db.Model.FindEntityType(typeof(PrimitiveCollectionsItem))!.FindProperty(
                nameof(PrimitiveCollectionsItem.LabelSet))!;
        var comparer = property.GetValueComparer()!;

        var source = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Alpha" };
        var snapshot = (HashSet<string>)comparer.Snapshot(source);

        snapshot.Contains("alpha").Should().BeTrue();
    }

    [Fact]
    public async Task ReadOnlyDictionary_Materializes_AndSnapshotReturnsSameReference()
    {
        var readOnlyDictionaryOptionsBuilder =
            new DbContextOptionsBuilder<ReadOnlyDictionaryDbContext>();
        readOnlyDictionaryOptionsBuilder.UseDynamo(options => options.ServiceUrl(ServiceUrl));
        var options = readOnlyDictionaryOptionsBuilder.Options;

        await using var readOnlyDictionaryDb = new ReadOnlyDictionaryDbContext(options);
        var item =
            await readOnlyDictionaryDb.Items.FirstAsync(x => x.Pk == "ITEM#A", CancellationToken);

        item.ScoresByCategory["math"].Should().Be(10);
        item.ScoresByCategory.Should().BeOfType<ReadOnlyDictionary<string, int>>();

        var property =
            readOnlyDictionaryDb.Model.FindEntityType(typeof(ReadOnlyDictionaryItem))!.FindProperty(
                nameof(ReadOnlyDictionaryItem.ScoresByCategory))!;
        var comparer = property.GetValueComparer()!;
        var sample =
            new ReadOnlyDictionary<string, int>(
                new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });

        var snapshot = comparer.Snapshot(sample);
        snapshot.Should().BeSameAs(sample);
    }

    [Fact]
    public async Task ReadOnlyMemoryByte_Materializes_AndComparerSnapshotsByValue()
    {
        await Client.PutItemAsync(
            new PutItemRequest
            {
                TableName = PrimitiveCollectionsDynamoFixture.TableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["Pk"] = new() { S = "ITEM#ROM" },
                    ["Payload"] = new() { B = new MemoryStream([1, 2, 3]) },
                },
            },
            CancellationToken);

        var readOnlyMemoryOptionsBuilder = new DbContextOptionsBuilder<ReadOnlyMemoryDbContext>();
        readOnlyMemoryOptionsBuilder.UseDynamo(options => options.ServiceUrl(ServiceUrl));
        var options = readOnlyMemoryOptionsBuilder.Options;

        await using var readOnlyMemoryDb = new ReadOnlyMemoryDbContext(options);
        var item =
            await readOnlyMemoryDb.Items.FirstAsync(x => x.Pk == "ITEM#ROM", CancellationToken);
        item.Payload.ToArray().Should().Equal(1, 2, 3);

        var property =
            readOnlyMemoryDb.Model.FindEntityType(typeof(ReadOnlyMemoryItem))!.FindProperty(
                nameof(ReadOnlyMemoryItem.Payload))!;
        var comparer = property.GetValueComparer()!;

        var backingBytes = new byte[] { 4, 5, 6 };
        var value = new ReadOnlyMemory<byte>(backingBytes);
        var snapshot = (ReadOnlyMemory<byte>)comparer.Snapshot(value);

        backingBytes[0] = 9;
        snapshot.ToArray().Should().Equal(4, 5, 6);
    }

    private sealed class ReadOnlyDictionaryDbContext(
        DbContextOptions<ReadOnlyDictionaryDbContext> options) : DbContext(options)
    {
        public DbSet<ReadOnlyDictionaryItem> Items => Set<ReadOnlyDictionaryItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder
                .Entity<ReadOnlyDictionaryItem>()
                .ToTable(PrimitiveCollectionsDynamoFixture.TableName)
                .HasKey(x => x.Pk);
    }

    private sealed class ReadOnlyDictionaryItem
    {
        public string Pk { get; set; } = null!;

        public ReadOnlyDictionary<string, int> ScoresByCategory { get; set; } =
            new(new Dictionary<string, int>());
    }

    private sealed class ReadOnlyMemoryDbContext(DbContextOptions<ReadOnlyMemoryDbContext> options)
        : DbContext(options)
    {
        public DbSet<ReadOnlyMemoryItem> Items => Set<ReadOnlyMemoryItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder
                .Entity<ReadOnlyMemoryItem>()
                .ToTable(PrimitiveCollectionsDynamoFixture.TableName)
                .HasKey(x => x.Pk);
    }

    private sealed class ReadOnlyMemoryItem
    {
        public string Pk { get; set; } = null!;

        public ReadOnlyMemory<byte> Payload { get; set; }
    }
}
