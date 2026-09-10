using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

/// <summary>
///     Pins dictionary write/read symmetry for null values: the write path emits
///     <c>NULL</c> wire entries for nullable values, so the read path must accept them.
/// </summary>
public class DynamoDictionaryNullValueTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ReadDictionary_NullableValueEntry_MaterializesNull()
    {
        var mapping = CreateMapping<Dictionary<string, int?>>(nameof(NullValueEntity.Charges));
        var property = FindProperty(nameof(NullValueEntity.Charges));

        var attributeValue = new AttributeValue
        {
            M = new Dictionary<string, AttributeValue>
            {
                ["tax"] = new() { NULL = true }, ["fee"] = new() { N = "5" }
            }
        };

        object? resultObject;
        try
        {
            resultObject = mapping.ReaderWriter!.ReadObject(
                attributeValue,
                nameof(NullValueEntity.Charges),
                required: false,
                property);
        }
        catch (Exception ex)
        {
            throw new Exception(ex.ToString());
        }

        var result = (Dictionary<string, int?>?)resultObject;

        result.Should().NotBeNull();
        result!["tax"].Should().BeNull();
        result["fee"].Should().Be(5);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ReadDictionary_StringValueEntry_MaterializesNull()
    {
        var mapping = CreateMapping<Dictionary<string, string>>(nameof(NullValueEntity.Labels));
        var property = FindProperty(nameof(NullValueEntity.Labels));

        var attributeValue = new AttributeValue
        {
            M = new Dictionary<string, AttributeValue>
            {
                ["a"] = new() { NULL = true }, ["b"] = new() { S = "x" }
            }
        };

        var result = (Dictionary<string, string?>?)mapping.ReaderWriter!.ReadObject(
            attributeValue,
            nameof(NullValueEntity.Labels),
            required: false,
            property);

        result.Should().NotBeNull();
        result!["a"].Should().BeNull();
        result["b"].Should().Be("x");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ReadDictionary_NonNullableValueEntry_Throws()
    {
        var mapping = CreateMapping<Dictionary<string, int>>(nameof(NullValueEntity.Strict));
        var property = FindProperty(nameof(NullValueEntity.Strict));

        var attributeValue = new AttributeValue
        {
            M = new Dictionary<string, AttributeValue> { ["count"] = new() { NULL = true } }
        };

        var act = () => mapping.ReaderWriter!.ReadObject(
            attributeValue,
            nameof(NullValueEntity.Strict),
            required: false,
            property);

        act.Should().Throw<InvalidOperationException>().WithMessage("*did not contain a value*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Dictionary_NullableValue_RoundTripsThroughBoxedBoundary()
    {
        var mapping = CreateMapping<Dictionary<string, int?>>(nameof(NullValueEntity.Charges));
        var source = new Dictionary<string, int?> { ["tax"] = null, ["fee"] = 3 };

        var attributeValue = mapping.CreateAttributeValue(source, typeof(Dictionary<string, int?>));

        attributeValue.M["tax"].NULL.Should().BeTrue();
        attributeValue.M["fee"].N.Should().Be("3");

        var property = FindProperty(nameof(NullValueEntity.Charges));
        var roundTripped = (Dictionary<string, int?>?)mapping.ReaderWriter!.ReadObject(
            attributeValue,
            nameof(NullValueEntity.Charges),
            required: false,
            property);

        roundTripped.Should().BeEquivalentTo(source);
    }

    private static DynamoTypeMapping CreateMapping<TProperty>(string propertyName)
        => (DynamoTypeMapping)FindProperty(propertyName).GetTypeMapping();

    private static IProperty FindProperty(string propertyName)
    {
        using var context = CreateContext();
        return context.Model.FindEntityType(typeof(NullValueEntity))!.FindProperty(propertyName)!;
    }

    private static NullValueContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<NullValueContext>();
        optionsBuilder
            .UseDynamo()
            .ConfigureWarnings(w
                => w
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected));
        return new NullValueContext(optionsBuilder.Options);
    }

    private sealed class NullValueContext(DbContextOptions<NullValueContext> options) : DbContext(
        options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NullValueEntity>(builder =>
            {
                builder.ToTable("NullValueItems");
                builder.HasPartitionKey(x => x.Pk);
            });
    }

    private sealed class NullValueEntity
    {
        public string Pk { get; set; } = null!;

        public Dictionary<string, int?> Charges { get; set; } = [];

        public Dictionary<string, string> Labels { get; set; } = [];

        public Dictionary<string, int> Strict { get; set; } = [];
    }
}
