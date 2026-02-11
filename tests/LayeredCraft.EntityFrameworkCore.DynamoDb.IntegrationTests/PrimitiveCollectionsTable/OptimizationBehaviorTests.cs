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

    [Fact]
    public async Task DerivedCollectionTypes_Materialize_FromSupportedInterfaces()
    {
        var optionsBuilder = new DbContextOptionsBuilder<DerivedCollectionDbContext>();
        optionsBuilder.UseDynamo(options => options.ServiceUrl(ServiceUrl));

        await using var db = new DerivedCollectionDbContext(optionsBuilder.Options);
        var item = await db.Items.FirstAsync(x => x.Pk == "ITEM#A", CancellationToken);

        item.Tags.Should().BeOfType<MyList>();
        item.ScoresByCategory.Should().BeOfType<MyDict>();

        item.Tags.Should().Contain("alpha");
        item.ScoresByCategory["math"].Should().Be(10);
        item.LabelSet.Should().Contain("common");
    }

    [Fact]
    public void NullableDictionaryValues_ComparerHandlesNulls()
    {
        var optionsBuilder = new DbContextOptionsBuilder<NullableDictionaryDbContext>();
        optionsBuilder.UseDynamo(options => options.ServiceUrl(ServiceUrl));

        using var db = new NullableDictionaryDbContext(optionsBuilder.Options);

        var property =
            db.Model.FindEntityType(typeof(NullableDictionaryItem))!.FindProperty(
                nameof(NullableDictionaryItem.ScoresNullable))!;
        var comparer = property.GetValueComparer()!;

        var left = new Dictionary<string, int?> { ["a"] = 1, ["b"] = null };
        var right = new Dictionary<string, int?> { ["b"] = null, ["a"] = 1 };
        comparer.Equals(left, right).Should().BeTrue();
    }

    [Fact]
    public async Task InterfaceCollectionProperties_Materialize_FromSupportedInterfaces()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InterfaceCollectionsDbContext>();
        optionsBuilder.UseDynamo(options => options.ServiceUrl(ServiceUrl));

        await using var db = new InterfaceCollectionsDbContext(optionsBuilder.Options);
        var item = await db.Items.FirstAsync(x => x.Pk == "ITEM#A", CancellationToken);

        item.Tags.Should().Contain("alpha");
        item.Tags.Should().Contain("beta");
        item.RatingSet.Should().Contain(1);
        item.RatingSet.Should().Contain(2);
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

    private sealed class DerivedCollectionDbContext(
        DbContextOptions<DerivedCollectionDbContext> options) : DbContext(options)
    {
        public DbSet<DerivedCollectionItem> Items => Set<DerivedCollectionItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder
                .Entity<DerivedCollectionItem>()
                .ToTable(PrimitiveCollectionsDynamoFixture.TableName)
                .HasKey(x => x.Pk);
    }

    private sealed class DerivedCollectionItem
    {
        public string Pk { get; set; } = null!;

        public MyList Tags { get; set; } = [];

        public MyDict ScoresByCategory { get; set; } = [];

        public HashSet<string> LabelSet { get; set; } = [];
    }

    private sealed class MyList : List<string>;

    private sealed class MyDict : Dictionary<string, int>;

    private sealed class NullableDictionaryDbContext(
        DbContextOptions<NullableDictionaryDbContext> options) : DbContext(options)
    {
        public DbSet<NullableDictionaryItem> Items => Set<NullableDictionaryItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder
                .Entity<NullableDictionaryItem>()
                .ToTable(PrimitiveCollectionsDynamoFixture.TableName)
                .HasKey(x => x.Pk);
    }

    private sealed class NullableDictionaryItem
    {
        public string Pk { get; set; } = null!;

        public Dictionary<string, int?> ScoresNullable { get; set; } = new();
    }

    private sealed class InterfaceCollectionsDbContext(
        DbContextOptions<InterfaceCollectionsDbContext> options) : DbContext(options)
    {
        public DbSet<InterfaceCollectionsItem> Items => Set<InterfaceCollectionsItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder
                .Entity<InterfaceCollectionsItem>()
                .ToTable(PrimitiveCollectionsDynamoFixture.TableName)
                .HasKey(x => x.Pk);
    }

    private sealed class InterfaceCollectionsItem
    {
        public string Pk { get; set; } = null!;

        public IEnumerable<string> Tags { get; set; } = [];

        public IReadOnlySet<int> RatingSet { get; set; } = new HashSet<int>();
    }
}
