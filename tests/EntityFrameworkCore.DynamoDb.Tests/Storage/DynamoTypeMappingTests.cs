using System.Globalization;
using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.ChangeTracking.Internal;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

public class DynamoTypeMappingTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateAttributeValue_UsesComposedConverter_ForGuid()
    {
        using var context = CreateContext();

        var property = GetProperty(context, nameof(SerializationEntity.ConvertedGuid));
        var mapping = (DynamoTypeMapping)property.GetTypeMapping();
        var value = Guid.Parse("67f0d1b7-e95c-4b26-972d-5c06455d8a53");

        var attributeValue = mapping.CreateAttributeValue(value);

        attributeValue.S.Should().Be(value.ToString());
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void GenerateConstant_UsesInvariantCulture_ForDecimal()
    {
        using var context = CreateContext();
        var property = GetProperty(context, nameof(SerializationEntity.Price));
        var mapping = (DynamoTypeMapping)property.GetTypeMapping();

        using var _ = new CultureScope("de-DE");

        mapping.GenerateConstant(9.99m).Should().Be("9.99");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateAttributeValue_SerializesPrimitiveCollections()
    {
        using var context = CreateContext();

        var scoresMapping =
            (DynamoTypeMapping)GetProperty(context, nameof(SerializationEntity.Scores))
                .GetTypeMapping();
        var chargesMapping =
            (DynamoTypeMapping)GetProperty(context, nameof(SerializationEntity.Charges))
                .GetTypeMapping();
        var flagsMapping =
            (DynamoTypeMapping)GetProperty(context, nameof(SerializationEntity.Flags))
                .GetTypeMapping();

        var scores = scoresMapping.CreateAttributeValue(new List<int> { 1, 2, 3 });
        var charges = chargesMapping.CreateAttributeValue(
            new Dictionary<string, decimal> { ["shipping"] = 9.99m, ["tax"] = 1.25m });
        var flags = flagsMapping.CreateAttributeValue(new HashSet<int> { 7, 11 });

        scores.L.Select(x => x.N).Should().Equal("1", "2", "3");
        charges.M["shipping"].N.Should().Be("9.99");
        charges.M["tax"].N.Should().Be("1.25");
        flags.NS.Should().BeEquivalentTo("7", "11");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateReadExpression_RoundTripsConvertedAndCollectionValues()
    {
        using var context = CreateContext();

        var guidProperty = GetProperty(context, nameof(SerializationEntity.ConvertedGuid));
        var guidMapping = (DynamoTypeMapping)guidProperty.GetTypeMapping();
        var scoresProperty = GetProperty(context, nameof(SerializationEntity.Scores));
        var scoresMapping = (DynamoTypeMapping)scoresProperty.GetTypeMapping();

        var guidReader = CompileReader<Guid>(guidMapping, guidProperty, "Entity.ConvertedGuid");
        var scoresReader = CompileReader<List<int>>(scoresMapping, scoresProperty, "Entity.Scores");
        var guid = Guid.Parse("f5d0b6aa-28d5-4a0d-9afc-4cb772dbdf18");

        guidReader(new AttributeValue { S = guid.ToString() }).Should().Be(guid);
        scoresReader(
                new AttributeValue
                {
                    L =
                    [
                        new AttributeValue { N = "1" },
                        new AttributeValue { N = "2" },
                        new AttributeValue { N = "3" }
                    ]
                })
            .Should()
            .Equal(1, 2, 3);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateAttributeValue_AndReadExpression_SupportNullableCollectionElements()
    {
        using var context = CreateContext();

        var optionalScoresProperty =
            GetProperty(context, nameof(SerializationEntity.OptionalScores));
        var optionalScoresMapping = (DynamoTypeMapping)optionalScoresProperty.GetTypeMapping();
        var optionalScoresReader = CompileReader<List<int?>>(
            optionalScoresMapping,
            optionalScoresProperty,
            "Entity.OptionalScores");

        var attributeValue =
            optionalScoresMapping.CreateAttributeValue(new List<int?> { null, 42 });

        attributeValue.L.Should().HaveCount(2);
        attributeValue.L[0].NULL.Should().BeTrue();
        attributeValue.L[1].N.Should().Be("42");
        optionalScoresReader(attributeValue).Should().Equal(null, 42);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void PrimitiveCollections_UseTheirPropertySpecificElementComparers()
    {
        var caseInsensitive = CreateStringComparer(StringComparer.OrdinalIgnoreCase);
        var ordinal = CreateStringComparer(StringComparer.Ordinal);

        AssertDistinctComparers(
            new ListValueComparer<List<string>, string>(caseInsensitive),
            new ListValueComparer<List<string>, string>(ordinal),
            new List<string> { "alpha" },
            new List<string> { "ALPHA" });
        AssertDistinctComparers(
            new SetValueComparer<HashSet<string>, string>(caseInsensitive),
            new SetValueComparer<HashSet<string>, string>(ordinal),
            new HashSet<string> { "alpha" },
            new HashSet<string> { "ALPHA" });
        AssertDistinctComparers(
            new StringDictionaryValueComparer<Dictionary<string, string>, string>(caseInsensitive),
            new StringDictionaryValueComparer<Dictionary<string, string>, string>(ordinal),
            new Dictionary<string, string> { ["name"] = "alpha" },
            new Dictionary<string, string> { ["name"] = "ALPHA" });
    }

    private static SerializationContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SerializationContext>();
        optionsBuilder
            .UseDynamo()
            .ConfigureWarnings(w
                => w
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected));
        return new SerializationContext(optionsBuilder.Options);
    }

    private static IProperty GetProperty(DbContext context, string propertyName)
        => context.Model.FindEntityType(typeof(SerializationEntity))!.FindProperty(propertyName)!;

    private static ValueComparer<string> CreateStringComparer(StringComparer comparer)
        => new(
            (left, right) => comparer.Equals(left, right),
            value => value == null ? 0 : comparer.GetHashCode(value),
            value => value);

    private static void AssertDistinctComparers<TCollection>(
        ValueComparer<TCollection> caseInsensitiveComparer,
        ValueComparer<TCollection> ordinalComparer,
        TCollection alpha,
        TCollection upperAlpha) where TCollection : class
    {
        caseInsensitiveComparer.Equals(alpha, upperAlpha).Should().BeTrue();
        ordinalComparer.Equals(alpha, upperAlpha).Should().BeFalse();
        caseInsensitiveComparer
            .GetHashCode(alpha)
            .Should()
            .Be(caseInsensitiveComparer.GetHashCode(upperAlpha));
        ordinalComparer.GetHashCode(alpha).Should().NotBe(ordinalComparer.GetHashCode(upperAlpha));
        caseInsensitiveComparer.Snapshot(alpha).Should().NotBeSameAs(alpha);
        caseInsensitiveComparer
            .Equals(caseInsensitiveComparer.Snapshot(alpha), upperAlpha)
            .Should()
            .BeTrue();
    }

    private static Func<AttributeValue, T> CompileReader<T>(
        DynamoTypeMapping mapping,
        IProperty property,
        string propertyPath)
    {
        var attributeValue = Expression.Parameter(typeof(AttributeValue), "attributeValue");
        var body = mapping.CreateReadExpression(attributeValue, propertyPath, true, property);
        return Expression.Lambda<Func<AttributeValue, T>>(body, attributeValue).Compile();
    }

    private sealed class SerializationContext(DbContextOptions<SerializationContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SerializationEntity>(builder =>
            {
                builder.ToTable("SerializationItems");
                builder.HasPartitionKey(x => x.Pk);
                builder.HasSortKey(x => x.Sk);
                builder.Property(x => x.ConvertedGuid).HasConversion<string>();
            });
    }

    private sealed class SerializationEntity
    {
        public string Pk { get; set; } = null!;

        public string Sk { get; set; } = null!;

        public Guid ConvertedGuid { get; set; }

        public decimal Price { get; set; }

        public List<int> Scores { get; set; } = [];

        public List<int?> OptionalScores { get; set; } = [];

        public Dictionary<string, decimal> Charges { get; set; } = [];

        public HashSet<int> Flags { get; set; } = [];
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
