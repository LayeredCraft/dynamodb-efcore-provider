using System.Globalization;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

/// <summary>
///     Pins the observable behavior of the boxed runtime serialization boundary
///     (<c>CreateAttributeValue</c> / <c>GenerateConstant</c>) so AOT-safe rewrites can be
///     verified for exact parity.
/// </summary>
public class DynamoBoxedSerializationTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateAttributeValue_NullWithoutConverter_IsNullAttribute()
    {
        var textMapping = CreateMapping<string>(nameof(BoxedEntity.Text));
        var flagMapping = CreateMapping<bool>(nameof(BoxedEntity.Flag));
        var countMapping = CreateMapping<int>(nameof(BoxedEntity.Count));
        var optionalMapping = CreateMapping<int?>(nameof(BoxedEntity.OptionalCount));

        textMapping.CreateAttributeValue(null).NULL.Should().BeTrue();
        flagMapping.CreateAttributeValue(null).NULL.Should().BeTrue();
        countMapping.CreateAttributeValue(null).NULL.Should().BeTrue();
        optionalMapping.CreateAttributeValue(null).NULL.Should().BeTrue();

        textMapping.CreateAttributeValue(null, typeof(string)).NULL.Should().BeTrue();
        countMapping.CreateAttributeValue(null, typeof(int)).NULL.Should().BeTrue();
        optionalMapping.CreateAttributeValue(null, typeof(int?)).NULL.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void GenerateConstant_NullWithoutConverter_IsNullLiteral()
    {
        var textMapping = CreateMapping<string>(nameof(BoxedEntity.Text));
        var countMapping = CreateMapping<int>(nameof(BoxedEntity.Count));

        textMapping.GenerateConstant(null).Should().Be("NULL");
        countMapping.GenerateConstant(null, typeof(int)).Should().Be("NULL");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateAttributeValue_ExactSourceType_UsesExpectedWireMember()
    {
        AssertWire(
            CreateMapping<string>(nameof(BoxedEntity.Text)),
            "value",
            typeof(string),
            attributeValue => attributeValue.S.Should().Be("value"));
        AssertWire(
            CreateMapping<bool>(nameof(BoxedEntity.Flag)),
            true,
            typeof(bool),
            attributeValue => attributeValue.BOOL.Should().BeTrue());
        AssertWire(
            CreateMapping<byte[]>(nameof(BoxedEntity.Blob)),
            new byte[] { 1, 2, 3 },
            typeof(byte[]),
            attributeValue => attributeValue.B!.ToArray().Should().Equal(1, 2, 3));

        AssertNumericWire<byte>(nameof(BoxedEntity.Tiny), (byte)5, "5");
        AssertNumericWire<sbyte>(nameof(BoxedEntity.Signed), (sbyte)-5, "-5");
        AssertNumericWire<short>(nameof(BoxedEntity.Small), (short)-32000, "-32000");
        AssertNumericWire<ushort>(nameof(BoxedEntity.UShort), (ushort)64000, "64000");
        AssertNumericWire<int>(nameof(BoxedEntity.Count), 42, "42");
        AssertNumericWire<uint>(nameof(BoxedEntity.UInt), 42u, "42");
        AssertNumericWire<long>(nameof(BoxedEntity.Big), 42L, "42");
        AssertNumericWire<ulong>(nameof(BoxedEntity.ULong), 42ul, "42");
        AssertNumericWire<float>(nameof(BoxedEntity.Float), 1.5f, "1.5");
        AssertNumericWire<double>(nameof(BoxedEntity.Double), 1.5d, "1.5");
        AssertNumericWire<decimal>(nameof(BoxedEntity.Price), 9.99m, "9.99");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateAttributeValue_NullableMapping_BoxedNonNullValue_WritesNumber()
    {
        var optionalMapping = CreateMapping<int?>(nameof(BoxedEntity.OptionalCount));

        var attributeValue = optionalMapping.CreateAttributeValue(42, typeof(int));

        attributeValue.N.Should().Be("42");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateAttributeValue_NumericPromotion_FormatsAsSourceType()
    {
        // The promotion path serializes using the runtime source type, not the mapping CLR type.
        AssertNumeric(
            CreateMapping<int>(nameof(BoxedEntity.Count)),
            (short)42,
            typeof(short),
            "42");
        AssertNumeric(CreateMapping<int>(nameof(BoxedEntity.Count)), 42L, typeof(long), "42");
        AssertNumeric(CreateMapping<double>(nameof(BoxedEntity.Double)), 42, typeof(int), "42");
        AssertNumeric(CreateMapping<decimal>(nameof(BoxedEntity.Price)), 42, typeof(int), "42");
        AssertNumeric(CreateMapping<short>(nameof(BoxedEntity.Small)), 70000, typeof(int), "70000");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void GenerateConstant_NumericPromotion_FormatsAsSourceType()
    {
        var countMapping = CreateMapping<int>(nameof(BoxedEntity.Count));

        countMapping.GenerateConstant((short)42, typeof(short)).Should().Be("42");
        countMapping.GenerateConstant(42L, typeof(long)).Should().Be("42");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateAttributeValue_NonNumericMismatch_ThrowsCoercionError()
    {
        var countMapping = CreateMapping<int>(nameof(BoxedEntity.Count));

        var act = () => countMapping.CreateAttributeValue("text", typeof(string));

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*'System.String' and 'System.Int32'*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void GenerateConstant_BinaryValue_IsNotSupported()
    {
        var blobMapping = CreateMapping<byte[]>(nameof(BoxedEntity.Blob));

        var act = () => blobMapping.GenerateConstant(new byte[] { 1 }, typeof(byte[]));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void GenerateConstant_ExactTypes_UsesExpectedLiteral()
    {
        CreateMapping<string>(nameof(BoxedEntity.Text))
            .GenerateConstant("o'brien", typeof(string))
            .Should()
            .Be("'o''brien'");
        CreateMapping<bool>(nameof(BoxedEntity.Flag))
            .GenerateConstant(true, typeof(bool))
            .Should()
            .Be("TRUE");
        CreateMapping<bool>(nameof(BoxedEntity.Flag))
            .GenerateConstant(false, typeof(bool))
            .Should()
            .Be("FALSE");
        CreateMapping<int>(nameof(BoxedEntity.Count))
            .GenerateConstant(42, typeof(int))
            .Should()
            .Be("42");
        CreateMapping<decimal>(nameof(BoxedEntity.Price))
            .GenerateConstant(9.99m, typeof(decimal))
            .Should()
            .Be("9.99");
        CreateMapping<Guid>(nameof(BoxedEntity.ConvertedGuid))
            .GenerateConstant(new Guid("67f0d1b7-e95c-4b26-972d-5c06455d8a53"), typeof(Guid))
            .Should()
            .Be("'67f0d1b7-e95c-4b26-972d-5c06455d8a53'");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateAttributeValue_NullableSourceType_UnboxesUnderlying()
    {
        // A boxed nullable value arrives as the boxed underlying; nullable source types must
        // dispatch the same way as their underlying types.
        AssertNumeric(
            CreateMapping<int>(nameof(BoxedEntity.Count)),
            (short)42,
            typeof(short?),
            "42");
        AssertNumeric(
            CreateMapping<int?>(nameof(BoxedEntity.OptionalCount)),
            (short)42,
            typeof(short?),
            "42");

        var optionalMapping = CreateMapping<int?>(nameof(BoxedEntity.OptionalCount));
        optionalMapping.CreateAttributeValue((short)42, typeof(short?)).N.Should().Be("42");

        var statusMapping = CreateMapping<BoxedStatus>(nameof(BoxedEntity.Status));
        statusMapping
            .CreateAttributeValue(BoxedStatus.Active, typeof(BoxedStatus?))
            .S
            .Should()
            .Be(nameof(BoxedStatus.Active));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void GenerateConstant_NullableSourceType_FormatsAsUnderlying()
    {
        var countMapping = CreateMapping<int>(nameof(BoxedEntity.Count));

        countMapping.GenerateConstant((short?)42, typeof(short?)).Should().Be("42");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateAttributeValue_ConvertedEnum_WritesStringWireValue()
    {
        var statusMapping = CreateMapping<BoxedStatus>(nameof(BoxedEntity.Status));

        var attributeValue = statusMapping.CreateAttributeValue(BoxedStatus.Active);

        attributeValue.S.Should().Be(nameof(BoxedStatus.Active));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateAttributeValue_RawEnum_WritesNumericWireValue()
    {
        var rawMapping = CreateMapping<BoxedStatus>(nameof(BoxedEntity.RawStatus));

        var attributeValue =
            rawMapping.CreateAttributeValue(BoxedStatus.Active, typeof(BoxedStatus));

        attributeValue.N.Should().Be("1");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateAttributeValue_ConvertedGuid_WritesStringWireValue()
    {
        var guidMapping = CreateMapping<Guid>(nameof(BoxedEntity.ConvertedGuid));
        var guid = new Guid("67f0d1b7-e95c-4b26-972d-5c06455d8a53");

        var attributeValue = guidMapping.CreateAttributeValue(guid);

        attributeValue.S.Should().Be(guid.ToString());
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateAttributeValue_ConverterWithConvertsNulls_ConvertsNull()
    {
        var nullTextMapping = CreateMapping<string>(nameof(BoxedEntity.NullText));

        var attributeValue = nullTextMapping.CreateAttributeValue(null);

        attributeValue.S.Should().Be("NULL-MARKER");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateAttributeValue_SerializesCollections_ThroughBoxedBoundary()
    {
        var scoresMapping = CreateMapping<List<int>>(nameof(BoxedEntity.Scores));
        var flagsMapping = CreateMapping<HashSet<int>>(nameof(BoxedEntity.Flags));
        var chargesMapping =
            CreateMapping<Dictionary<string, decimal>>(nameof(BoxedEntity.Charges));

        var scores =
            scoresMapping.CreateAttributeValue(new List<int> { 1, 2, 3 }, typeof(List<int>));
        var flags =
            flagsMapping.CreateAttributeValue(new HashSet<int> { 7, 11 }, typeof(HashSet<int>));
        var charges = chargesMapping.CreateAttributeValue(
            new Dictionary<string, decimal> { ["tax"] = 1.25m },
            typeof(Dictionary<string, decimal>));

        scores.L.Select(x => x.N).Should().Equal("1", "2", "3");
        flags.NS.Should().BeEquivalentTo("7", "11");
        charges.M["tax"].N.Should().Be("1.25");
    }

    private static void AssertWire<TValue>(
        DynamoTypeMapping mapping,
        TValue value,
        Type sourceType,
        Action<AttributeValue> assert)
    {
        var attributeValue = mapping.CreateAttributeValue(value, sourceType);
        assert(attributeValue);
    }

    private static void AssertNumeric<TValue>(
        DynamoTypeMapping mapping,
        TValue value,
        Type sourceType,
        string expectedNumber) where TValue : struct
    {
        var attributeValue = mapping.CreateAttributeValue(value, sourceType);
        attributeValue.N.Should().Be(expectedNumber);
    }

    private static void AssertNumericWire<TValue>(
        string propertyName,
        TValue value,
        string expectedNumber) where TValue : struct
    {
        var mapping = CreateMapping<TValue>(propertyName);
        var attributeValue = mapping.CreateAttributeValue(value, typeof(TValue));
        attributeValue.N.Should().Be(expectedNumber);
    }

    private static DynamoTypeMapping CreateMapping<TProperty>(string propertyName)
    {
        using var context = CreateContext();
        return (DynamoTypeMapping)context.Model.FindEntityType(typeof(BoxedEntity))!.FindProperty(
            propertyName)!.GetTypeMapping();
    }

    private static BoxedContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<BoxedContext>();
        optionsBuilder
            .UseDynamo()
            .ConfigureWarnings(w
                => w
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected));
        return new BoxedContext(optionsBuilder.Options);
    }

    private sealed class BoxedContext(DbContextOptions<BoxedContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<BoxedEntity>(builder =>
            {
                builder.ToTable("BoxedItems");
                builder.HasPartitionKey(x => x.Pk);
                builder.Property(x => x.ConvertedGuid).HasConversion<string>();
                builder.Property(x => x.Status).HasConversion<string>();
                builder
                    .Property(x => x.NullText)
                    .HasConversion(
                        new ValueConverter<string?, string>(
                            value => value ?? "NULL-MARKER",
                            value => value == "NULL-MARKER" ? null : value,
                            convertsNulls: true));
            });
    }

    private sealed class BoxedEntity
    {
        public string Pk { get; set; } = null!;

        public string Text { get; set; } = null!;

        public bool Flag { get; set; }

        public int Count { get; set; }

        public short Small { get; set; }

        public long Big { get; set; }

        public byte Tiny { get; set; }

        public sbyte Signed { get; set; }

        public ushort UShort { get; set; }

        public uint UInt { get; set; }

        public ulong ULong { get; set; }

        public float Float { get; set; }

        public double Double { get; set; }

        public decimal Price { get; set; }

        public int? OptionalCount { get; set; }

        public byte[] Blob { get; set; } = null!;

        public Guid ConvertedGuid { get; set; }

        public string? NullText { get; set; }

        public BoxedStatus Status { get; set; }

        public BoxedStatus RawStatus { get; set; }

        public List<int> Scores { get; set; } = [];

        public HashSet<int> Flags { get; set; } = [];

        public Dictionary<string, decimal> Charges { get; set; } = [];
    }

    public enum BoxedStatus
    {
        Active = 1
    }
}
