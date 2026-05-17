using System.Collections.ObjectModel;
using Amazon.DynamoDBv2.Model;
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
    public void BuildItem_EnumWithStringConverter_UsesStringAttributeValue()
    {
        using var db = new ConvertedEnumDbContext(
            new DbContextOptionsBuilder<ConvertedEnumDbContext>()
                .UseDynamo()
                .ConfigureWarnings(w
                    => w
                        .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                        .Ignore(DynamoEventId.ScanLikeQueryDetected))
                .Options);

        var entity = new ConvertedEnumEntity
        {
            Pk = "E#1",
            Sk = "E1",
            Status = ConvertedStatus.Active,
            OptionalStatus = ConvertedStatus.Inactive,
        };

        db.Add(entity);

        var serializer = db.GetService<DynamoEntityItemSerializerSource>();
        var updateEntry = (IUpdateEntry)db.Entry(entity).GetInfrastructure();

        var item = serializer.BuildItem(updateEntry);
        item["status"].S.Should().Be(nameof(ConvertedStatus.Active));
        item["status"].N.Should().BeNull();
        item["optionalStatus"].S.Should().Be(nameof(ConvertedStatus.Inactive));
        item["optionalStatus"].N.Should().BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void BuildItem_NullableEnumWithStringConverter_UsesNullAttributeValueForNull()
    {
        using var db = new ConvertedEnumDbContext(
            new DbContextOptionsBuilder<ConvertedEnumDbContext>()
                .UseDynamo()
                .ConfigureWarnings(w
                    => w
                        .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                        .Ignore(DynamoEventId.ScanLikeQueryDetected))
                .Options);

        var entity = new ConvertedEnumEntity
        {
            Pk = "E#2",
            Sk = "E2",
            Status = ConvertedStatus.Active,
            OptionalStatus = null,
        };

        db.Add(entity);

        var serializer = db.GetService<DynamoEntityItemSerializerSource>();
        var updateEntry = (IUpdateEntry)db.Entry(entity).GetInfrastructure();

        var item = serializer.BuildItem(updateEntry);
        item["optionalStatus"].NULL.Should().BeTrue();
        item["optionalStatus"].S.Should().BeNull();
        item["optionalStatus"].N.Should().BeNull();
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
            Pk = "L#1",
            Sk = "L1",
            NullableStatuses = [NullableStatus.Active, null],
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
                ["ok"] = NullableStatus.Active,
                ["missing"] = null,
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
            Pk = "S#1",
            Sk = "S1",
            NullableStatusSet = [NullableStatus.Active, null],
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
    public void BuildItem_NullableSetProperty_WhenNull_SerializesAsNull()
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
            Pk = "OS#NULL",
            Sk = "OS1",
            NullableStatusSet = [NullableStatus.Active],
            OptionalStatusSet = null,
        };

        db.Add(entity);

        var serializer = db.GetService<DynamoEntityItemSerializerSource>();
        var updateEntry = (IUpdateEntry)db.Entry(entity).GetInfrastructure();

        var item = serializer.BuildItem(updateEntry);

        item["optionalStatusSet"].NULL.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void BuildItem_NullableSetProperty_WhenEmpty_Throws()
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
            Pk = "OS#EMPTY",
            Sk = "OS2",
            NullableStatusSet = [NullableStatus.Active],
            OptionalStatusSet = [],
        };

        db.Add(entity);

        var serializer = db.GetService<DynamoEntityItemSerializerSource>();
        var updateEntry = (IUpdateEntry)db.Entry(entity).GetInfrastructure();

        var act = () => serializer.BuildItem(updateEntry);

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "DynamoDB sets cannot be empty; use a null property or a non-empty collection.");
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
            Pk = "C#1",
            Sk = "C1",
            Contacts = [new ComplexContact { Value = "ok" }, null!],
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void BuildItem_ComplexTypeWithScalarCollections_SerializesListsAndBinary()
    {
        using var db = new ComplexScalarCollectionDbContext(
            new DbContextOptionsBuilder<ComplexScalarCollectionDbContext>()
                .UseDynamo()
                .ConfigureWarnings(w
                    => w
                        .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                        .Ignore(DynamoEventId.ScanLikeQueryDetected))
                .Options);

        var entity = new ComplexScalarCollectionEntity
        {
            Pk = "SC#1",
            Sk = "SC1",
            Details = new ComplexScalarCollectionDetails
            {
                Members = ["m1", "m2"],
                Scores = [1, 2],
                OptionalScores = [3, null],
                Tags = ["red", "blue"],
                Counts =
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        ["a"] = 1,
                        ["b"] = 2,
                    },
                LongCounts =
                    new ReadOnlyDictionary<string, long>(
                        new Dictionary<string, long>(StringComparer.Ordinal)
                        {
                            ["big"] = 42
                        }),
                Flags =
                    new Dictionary<string, bool>(StringComparer.Ordinal)
                    {
                        ["enabled"] = true
                    },
                OptionalFlags =
                    new ReadOnlyDictionary<string, bool?>(
                        new Dictionary<string, bool?>(StringComparer.Ordinal)
                        {
                            ["yes"] = true,
                            ["unknown"] = null,
                        }),
                UnsignedCounts =
                    new ReadOnlyDictionary<string, uint>(
                        new Dictionary<string, uint>(StringComparer.Ordinal) { ["u"] = 7 }),
                Doubles =
                    new Dictionary<string, double?>(StringComparer.Ordinal)
                    {
                        ["pi"] = 3.14,
                        ["missing"] = null,
                    },
                BinaryByName =
                    new Dictionary<string, byte[]>(StringComparer.Ordinal)
                    {
                        ["blob"] = [4, 5, 6],
                    },
                Payload = [1, 2, 3],
            },
        };

        db.Add(entity);

        var serializer = db.GetService<DynamoEntityItemSerializerSource>();
        var updateEntry = (IUpdateEntry)db.Entry(entity).GetInfrastructure();

        var item = serializer.BuildItem(updateEntry);
        var details = item["details"].M;

        details["members"].L.Select(x => x.S).Should().Equal("m1", "m2");
        details["scores"].L.Select(x => x.N).Should().Equal("1", "2");
        details["optionalScores"].L[0].N.Should().Be("3");
        details["optionalScores"].L[1].NULL.Should().BeTrue();
        details["tags"].SS.Should().BeEquivalentTo("red", "blue");
        details["counts"].M["a"].N.Should().Be("1");
        details["counts"].M["b"].N.Should().Be("2");
        details["longCounts"].M["big"].N.Should().Be("42");
        details["flags"].M["enabled"].BOOL.Should().BeTrue();
        details["optionalFlags"].M["yes"].BOOL.Should().BeTrue();
        details["optionalFlags"].M["unknown"].NULL.Should().BeTrue();
        details["unsignedCounts"].M["u"].N.Should().Be("7");
        details["doubles"].M["pi"].N.Should().Be("3.14");
        details["doubles"].M["missing"].NULL.Should().BeTrue();
        details["binaryByName"].M["blob"].B.ToArray().Should().Equal(4, 5, 6);
        details["payload"].B.ToArray().Should().Equal(1, 2, 3);
        details["payload"].L.Should().BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void BuildItem_RootAndComplexScalarCollections_UseSameWireValues()
    {
        using var db = new ComplexScalarCollectionDbContext(
            new DbContextOptionsBuilder<ComplexScalarCollectionDbContext>()
                .UseDynamo()
                .ConfigureWarnings(w
                    => w
                        .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                        .Ignore(DynamoEventId.ScanLikeQueryDetected))
                .Options);

        var root = new RootScalarCollectionEntity
        {
            Pk = "ROOT#1",
            Sk = "ROOT1",
            Members = ["m1", "m2"],
            Scores = [1, 2],
            OptionalScores = [3, null],
            Tags = ["red", "blue"],
            Counts =
                new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 1, ["b"] = 2 },
            LongCounts =
                new ReadOnlyDictionary<string, long>(
                    new Dictionary<string, long>(StringComparer.Ordinal) { ["big"] = 42 }),
            Flags = new Dictionary<string, bool>(StringComparer.Ordinal) { ["enabled"] = true },
            OptionalFlags =
                new ReadOnlyDictionary<string, bool?>(
                    new Dictionary<string, bool?>(StringComparer.Ordinal)
                    {
                        ["yes"] = true,
                        ["unknown"] = null,
                    }),
            UnsignedCounts =
                new ReadOnlyDictionary<string, uint>(
                    new Dictionary<string, uint>(StringComparer.Ordinal) { ["u"] = 7 }),
            Doubles =
                new Dictionary<string, double?>(StringComparer.Ordinal)
                {
                    ["pi"] = 3.14,
                    ["missing"] = null,
                },
            BinaryByName =
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    ["blob"] = [4, 5, 6],
                },
            Payload = [1, 2, 3],
        };

        var complex = new ComplexScalarCollectionEntity
        {
            Pk = "COMPLEX#1",
            Sk = "COMPLEX1",
            Details = new ComplexScalarCollectionDetails
            {
                Members = root.Members,
                Scores = root.Scores,
                OptionalScores = root.OptionalScores,
                Tags = root.Tags,
                Counts = root.Counts,
                LongCounts = root.LongCounts,
                Flags = root.Flags,
                OptionalFlags = root.OptionalFlags,
                UnsignedCounts = root.UnsignedCounts,
                Doubles = root.Doubles,
                BinaryByName = root.BinaryByName,
                Payload = root.Payload,
                ConvertedCodes = new ConvertedCodeList("a,b"),
            },
        };

        db.AddRange(root, complex);

        var serializer = db.GetService<DynamoEntityItemSerializerSource>();
        var rootItem = serializer.BuildItem((IUpdateEntry)db.Entry(root).GetInfrastructure());
        var details =
            serializer.BuildItem((IUpdateEntry)db.Entry(complex).GetInfrastructure())["details"].M;

        foreach (var attributeName in new[]
            {
                "members",
                "scores",
                "optionalScores",
                "tags",
                "counts",
                "longCounts",
                "flags",
                "optionalFlags",
                "unsignedCounts",
                "doubles",
                "binaryByName",
                "payload",
            })
        {
            AssertAttributeValuesEquivalent(rootItem[attributeName], details[attributeName]);
        }
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void BuildItem_ComplexTypeWithPropertyConverterReturningCollection_SerializesList()
    {
        using var db = new ComplexScalarCollectionDbContext(
            new DbContextOptionsBuilder<ComplexScalarCollectionDbContext>()
                .UseDynamo()
                .ConfigureWarnings(w
                    => w
                        .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                        .Ignore(DynamoEventId.ScanLikeQueryDetected))
                .Options);

        var entity = new ComplexScalarCollectionEntity
        {
            Pk = "SC#2",
            Sk = "SC2",
            Details = new ComplexScalarCollectionDetails
            {
                ConvertedCodes = new ConvertedCodeList("a,b"),
            },
        };

        db.Add(entity);

        var serializer = db.GetService<DynamoEntityItemSerializerSource>();
        var updateEntry = (IUpdateEntry)db.Entry(entity).GetInfrastructure();

        var item = serializer.BuildItem(updateEntry);
        item["details"].M["convertedCodes"].L.Select(x => x.S).Should().Equal("a", "b");
    }

    private static void AssertAttributeValuesEquivalent(
        AttributeValue expected,
        AttributeValue actual)
    {
        actual.S.Should().Be(expected.S);
        actual.N.Should().Be(expected.N);
        actual.BOOL.Should().Be(expected.BOOL);
        actual.NULL.Should().Be(expected.NULL);
        actual.SS.Should().BeEquivalentTo(expected.SS);
        actual.NS.Should().BeEquivalentTo(expected.NS);
        actual.B?.ToArray().Should().Equal(expected.B?.ToArray() ?? []);
        actual
            .BS
            ?.Select(x => x.ToArray())
            .Should()
            .BeEquivalentTo(expected.BS?.Select(x => x.ToArray()) ?? []);

        actual.L?.Should().HaveCount(expected.L?.Count ?? 0);
        if (expected.L is not null)
            for (var i = 0; i < expected.L.Count; i++)
                AssertAttributeValuesEquivalent(expected.L[i], actual.L![i]);

        actual.M?.Keys.Should().BeEquivalentTo(expected.M?.Keys ?? Enumerable.Empty<string>());
        if (expected.M is not null)
            foreach (var (key, expectedValue) in expected.M)
                AssertAttributeValuesEquivalent(expectedValue, actual.M![key]);
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

    private enum ConvertedStatus
    {
        Active = 1,
        Inactive = 2,
    }

    private sealed class ConvertedEnumEntity
    {
        public string Pk { get; set; } = null!;
        public string Sk { get; set; } = null!;
        public ConvertedStatus Status { get; set; }
        public ConvertedStatus? OptionalStatus { get; set; }
    }

    private sealed class NullableCollectionEntity
    {
        public string Pk { get; set; } = null!;
        public string Sk { get; set; } = null!;
        public List<NullableStatus?> NullableStatuses { get; set; } = [];

        public Dictionary<string, NullableStatus?> NullableStatusByCode { get; set; } =
            new(StringComparer.Ordinal);

        public HashSet<NullableStatus?>? NullableStatusSet { get; set; }

        public HashSet<NullableStatus>? OptionalStatusSet { get; set; }
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

    private sealed class ComplexScalarCollectionEntity
    {
        public string Pk { get; set; } = null!;
        public string Sk { get; set; } = null!;
        public ComplexScalarCollectionDetails Details { get; set; } = new();
    }

    private sealed class RootScalarCollectionEntity
    {
        public string Pk { get; set; } = null!;
        public string Sk { get; set; } = null!;
        public string[] Members { get; set; } = [];
        public List<int> Scores { get; set; } = [];
        public List<int?> OptionalScores { get; set; } = [];
        public HashSet<string>? Tags { get; set; }
        public Dictionary<string, int> Counts { get; set; } = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, long> LongCounts { get; set; } =
            new ReadOnlyDictionary<string, long>(new Dictionary<string, long>());

        public Dictionary<string, bool> Flags { get; set; } = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, bool?> OptionalFlags { get; set; } =
            new ReadOnlyDictionary<string, bool?>(new Dictionary<string, bool?>());

        public IReadOnlyDictionary<string, uint> UnsignedCounts { get; set; } =
            new ReadOnlyDictionary<string, uint>(new Dictionary<string, uint>());

        public Dictionary<string, double?> Doubles { get; set; } = new(StringComparer.Ordinal);

        public Dictionary<string, byte[]> BinaryByName { get; set; } = new(StringComparer.Ordinal);

        public byte[] Payload { get; set; } = [];
    }

    private sealed class ComplexScalarCollectionDetails
    {
        public string[] Members { get; set; } = [];
        public List<int> Scores { get; set; } = [];
        public List<int?> OptionalScores { get; set; } = [];
        public HashSet<string>? Tags { get; set; }
        public Dictionary<string, int> Counts { get; set; } = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, long> LongCounts { get; set; } =
            new ReadOnlyDictionary<string, long>(new Dictionary<string, long>());

        public Dictionary<string, bool> Flags { get; set; } = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, bool?> OptionalFlags { get; set; } =
            new ReadOnlyDictionary<string, bool?>(new Dictionary<string, bool?>());

        public IReadOnlyDictionary<string, uint> UnsignedCounts { get; set; } =
            new ReadOnlyDictionary<string, uint>(new Dictionary<string, uint>());

        public Dictionary<string, double?> Doubles { get; set; } = new(StringComparer.Ordinal);

        public Dictionary<string, byte[]> BinaryByName { get; set; } = new(StringComparer.Ordinal);

        public byte[] Payload { get; set; } = [];
        public ConvertedCodeList ConvertedCodes { get; set; } = new(string.Empty);
    }

    private sealed record ConvertedCodeList(string Value);

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

    private sealed class ConvertedEnumDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ConvertedEnumEntity> Items => Set<ConvertedEnumEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConvertedEnumEntity>(b =>
            {
                b.ToTable("ConvertedEnums");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
                b.Property(x => x.Status).HasAttributeName("status").HasConversion<string>();
                b
                    .Property(x => x.OptionalStatus)
                    .HasAttributeName("optionalStatus")
                    .HasConversion<string>();
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
                b.Property(x => x.OptionalStatusSet).IsRequired(false);
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

    private sealed class ComplexScalarCollectionDbContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<ComplexScalarCollectionEntity> Items => Set<ComplexScalarCollectionEntity>();
        public DbSet<RootScalarCollectionEntity> RootItems => Set<RootScalarCollectionEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RootScalarCollectionEntity>(b =>
            {
                b.ToTable("RootScalarCollections");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
            });

            modelBuilder.Entity<ComplexScalarCollectionEntity>(b =>
            {
                b.ToTable("ComplexScalarCollections");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
                b.ComplexProperty(
                    x => x.Details,
                    details =>
                    {
                        details
                            .Property(x => x.ConvertedCodes)
                            .HasConversion(
                                codes => codes.Value.Length == 0
                                    ? Array.Empty<string>()
                                    : codes.Value.Split(',', StringSplitOptions.RemoveEmptyEntries),
                                values => new ConvertedCodeList(string.Join(",", values)));
                    });
            });
        }
    }
}
