using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

/// <summary>Unit tests for write-path error handling in the serializer source and helpers.</summary>
public class DynamoEntityItemSerializerSourceTests
{
    // ──────────────────────────────────────────────────────────────────────────────
    //  Table name sanitization
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     <c>BuildInsertStatement</c> must reject a table name containing a double-quote because
    ///     that character would break the PartiQL identifier syntax.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_TableNameWithDoubleQuote_ThrowsArgumentException()
    {
        var options = new DbContextOptionsBuilder<QuotedTableDbContext>()
            .UseDynamo()
            .ConfigureWarnings(w
                => w
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected))
            .Options;

        await using var db = new QuotedTableDbContext(options);
        db.Gadgets.Add(new Gadget { Pk = "G#1", Sk = "G1" });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => db.SaveChangesAsync());

        ex.Message.Should().Contain("\"");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void BuildItem_ListWithNullableEnumElements_UsesNullAttributeValueForNullElement()
    {
        using var db = new NullableCollectionDbContext(
            new DbContextOptionsBuilder<NullableCollectionDbContext>()
                .UseDynamo()
                .ConfigureWarnings(w
                    => w
                        .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                        .Ignore(DynamoEventId.ScanLikeQueryDetected))
                .Options);

        var entity = new NullableCollectionEntity
        {
            Pk = "L#1", Sk = "L1", NullableStatuses = [NullableStatus.Active, null],
        };

        db.Add(entity);

        var serializer = db.GetService<DynamoEntityItemSerializerSource>();
        var updateEntry = (IUpdateEntry)db.Entry(entity).GetInfrastructure();

        var item = serializer.BuildItem(updateEntry);
        item["nullableStatuses"].L.Should().HaveCount(2);
        item["nullableStatuses"].L[0].N.Should().Be("1");
        item["nullableStatuses"].L[1].NULL.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void BuildItem_DictionaryWithNullableEnumValues_UsesNullAttributeValueForNullValue()
    {
        using var db = new NullableCollectionDbContext(
            new DbContextOptionsBuilder<NullableCollectionDbContext>()
                .UseDynamo()
                .ConfigureWarnings(w
                    => w
                        .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                        .Ignore(DynamoEventId.ScanLikeQueryDetected))
                .Options);

        var entity = new NullableCollectionEntity
        {
            Pk = "M#1",
            Sk = "M1",
            NullableStatusByCode = new Dictionary<string, NullableStatus?>
            {
                ["ok"] = NullableStatus.Active, ["missing"] = null,
            },
        };

        db.Add(entity);

        var serializer = db.GetService<DynamoEntityItemSerializerSource>();
        var updateEntry = (IUpdateEntry)db.Entry(entity).GetInfrastructure();

        var item = serializer.BuildItem(updateEntry);
        item["nullableStatusByCode"].M["ok"].N.Should().Be("1");
        item["nullableStatusByCode"].M["missing"].NULL.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void BuildItem_SetWithNullableEnumElements_ThrowsWhenElementIsNull()
    {
        using var db = new NullableCollectionDbContext(
            new DbContextOptionsBuilder<NullableCollectionDbContext>()
                .UseDynamo()
                .ConfigureWarnings(w
                    => w
                        .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                        .Ignore(DynamoEventId.ScanLikeQueryDetected))
                .Options);

        var entity = new NullableCollectionEntity
        {
            Pk = "S#1", Sk = "S1", NullableStatusSet = [NullableStatus.Active, null],
        };

        db.Add(entity);

        var serializer = db.GetService<DynamoEntityItemSerializerSource>();
        var updateEntry = (IUpdateEntry)db.Entry(entity).GetInfrastructure();

        var act = () => serializer.BuildItem(updateEntry);

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*DynamoDB sets cannot contain null*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void BuildItem_ComplexCollectionWithNullElement_ThrowsWhenElementIsNull()
    {
        using var db = new ComplexCollectionDbContext(
            new DbContextOptionsBuilder<ComplexCollectionDbContext>()
                .UseDynamo()
                .ConfigureWarnings(w
                    => w
                        .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                        .Ignore(DynamoEventId.ScanLikeQueryDetected))
                .Options);

        var entity = new ComplexCollectionEntity
        {
            Pk = "C#1", Sk = "C1", Contacts = [new ComplexContact { Value = "ok" }, null!],
        };

        db.Add(entity);

        var serializer = db.GetService<DynamoEntityItemSerializerSource>();
        var updateEntry = (IUpdateEntry)db.Entry(entity).GetInfrastructure();

        var act = () => serializer.BuildItem(updateEntry);

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Complex collection*contains null element*");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Fixtures
    // ──────────────────────────────────────────────────────────────────────────────

    private sealed class Gadget
    {
        public string Pk { get; set; } = null!;
        public string Sk { get; set; } = null!;
    }

    private enum NullableStatus
    {
        Active = 1,
        Inactive = 2,
    }

    private sealed class NullableCollectionEntity
    {
        public string Pk { get; set; } = null!;
        public string Sk { get; set; } = null!;
        public List<NullableStatus?> NullableStatuses { get; set; } = [];

        public Dictionary<string, NullableStatus?> NullableStatusByCode { get; set; } =
            new(StringComparer.Ordinal);

        public HashSet<NullableStatus?> NullableStatusSet { get; set; } = [];
    }

    private sealed class ComplexCollectionEntity
    {
        public string Pk { get; set; } = null!;
        public string Sk { get; set; } = null!;
        public List<ComplexContact> Contacts { get; set; } = [];
    }

    private sealed class ComplexContact
    {
        public string Value { get; set; } = null!;
    }

    private sealed class QuotedTableDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Gadget> Gadgets => Set<Gadget>();

        // Table name contains a double-quote which is illegal in the PartiQL identifier.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Gadget>(b =>
            {
                b.ToTable("Bad\"TableName");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
            });
    }

    private sealed class NullableCollectionDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<NullableCollectionEntity> Items => Set<NullableCollectionEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NullableCollectionEntity>(b =>
            {
                b.ToTable("NullableCollections");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
                b.Property(x => x.NullableStatuses);
                b.Property(x => x.NullableStatusByCode);
                b.Property(x => x.NullableStatusSet);
            });
    }

    private sealed class ComplexCollectionDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ComplexCollectionEntity> Items => Set<ComplexCollectionEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ComplexCollectionEntity>(b =>
            {
                b.ToTable("ComplexCollections");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
                b.ComplexCollection(x => x.Contacts);
            });
    }
}
