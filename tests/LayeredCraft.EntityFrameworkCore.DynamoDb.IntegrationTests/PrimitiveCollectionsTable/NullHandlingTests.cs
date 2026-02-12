using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public class NullHandlingTests(PrimitiveCollectionsDynamoFixture fixture)
    : PrimitiveCollectionsTestBase(fixture)
{
    /// <summary>Verifies missing optional dictionary properties materialize as null.</summary>
    [Fact]
    public async Task Materialization_AllowsMissingOptionalDictionaryProperty()
    {
        var item = CreateOptionalCollectionsItem(
            "ITEM#MISSING-OPTIONAL-DICT",
            true,
            true,
            false,
            false);

        await PutItemAsync(item);

        await using var context =
            new OptionalCollectionsContext(CreateOptionalCollectionsOptions());
        var result = await context.Items.FirstAsync(
            x => x.Pk == "ITEM#MISSING-OPTIONAL-DICT",
            CancellationToken);

        result.OptionalMap.Should().BeNull();
    }

    /// <summary>Verifies missing optional set properties materialize as null.</summary>
    [Fact]
    public async Task Materialization_AllowsMissingOptionalSetProperty()
    {
        var item = CreateOptionalCollectionsItem(
            "ITEM#MISSING-OPTIONAL-SET",
            true,
            false,
            true,
            false);

        await PutItemAsync(item);

        await using var context =
            new OptionalCollectionsContext(CreateOptionalCollectionsOptions());
        var result = await context.Items.FirstAsync(
            x => x.Pk == "ITEM#MISSING-OPTIONAL-SET",
            CancellationToken);

        result.OptionalSet.Should().BeNull();
    }

    /// <summary>Verifies DynamoDB NULL maps to null for optional collection properties.</summary>
    [Fact]
    public async Task Materialization_AllowsDynamoNullForOptionalCollectionProperties()
    {
        var item = CreateOptionalCollectionsItem("ITEM#NULL-OPTIONALS", true, true, true, true);

        await PutItemAsync(item);

        await using var context =
            new OptionalCollectionsContext(CreateOptionalCollectionsOptions());
        var result = await context.Items.FirstAsync(
            x => x.Pk == "ITEM#NULL-OPTIONALS",
            CancellationToken);

        result.OptionalList.Should().BeNull();
        result.OptionalSet.Should().BeNull();
        result.OptionalMap.Should().BeNull();
    }

    /// <summary>Verifies optional array properties materialize from DynamoDB list wire values.</summary>
    [Fact]
    public async Task Materialization_AllowsOptionalArrayProperty()
    {
        var item = CreateOptionalCollectionsItem("ITEM#OPTIONAL-ARRAY", true, true, true, false);
        item["OptionalArray"] = new AttributeValue
        {
            L = [new AttributeValue { S = "one" }, new AttributeValue { S = "two" }],
        };

        await PutItemAsync(item);

        await using var context =
            new OptionalCollectionsContext(CreateOptionalCollectionsOptions());
        var result = await context.Items.FirstAsync(
            x => x.Pk == "ITEM#OPTIONAL-ARRAY",
            CancellationToken);

        result.OptionalArray.Should().Equal("one", "two");
    }

    [Fact]
    public async Task Materialization_Throws_WhenRequiredListPropertyIsMissing()
    {
        var item = CreateValidTemplateItem("ITEM#BAD-MISSING-TAGS");
        item.Remove("Tags");

        await PutItemAsync(item);

        var act = async ()
            => await Db
                .Items.Where(x => x.Pk == "ITEM#BAD-MISSING-TAGS")
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*PrimitiveCollectionsItem*Tags*")
            .WithMessage("*not present*");
    }

    [Fact]
    public async Task Materialization_Throws_WhenRequiredMapPropertyIsMissing()
    {
        var item = CreateValidTemplateItem("ITEM#BAD-MISSING-SCORES");
        item.Remove("ScoresByCategory");

        await PutItemAsync(item);

        var act = async ()
            => await Db
                .Items.Where(x => x.Pk == "ITEM#BAD-MISSING-SCORES")
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*PrimitiveCollectionsItem*ScoresByCategory*")
            .WithMessage("*not present*");
    }

    [Fact]
    public async Task Materialization_Throws_WhenRequiredSetPropertyIsMissing()
    {
        var item = CreateValidTemplateItem("ITEM#BAD-MISSING-LABELSET");
        item.Remove("LabelSet");

        await PutItemAsync(item);

        var act = async ()
            => await Db
                .Items.Where(x => x.Pk == "ITEM#BAD-MISSING-LABELSET")
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*PrimitiveCollectionsItem*LabelSet*")
            .WithMessage("*not present*");
    }

    [Fact]
    public async Task Materialization_Throws_WhenRequiredListPropertyIsDynamoNull()
    {
        var item = CreateValidTemplateItem("ITEM#BAD-NULL-TAGS");
        item["Tags"] = new AttributeValue { NULL = true };

        await PutItemAsync(item);

        var act = async ()
            => await Db
                .Items.Where(x => x.Pk == "ITEM#BAD-NULL-TAGS")
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Tags*")
            .WithMessage("*DynamoDB NULL*");
    }

    [Fact]
    public async Task Materialization_AllowsMissingOptionalListProperty()
    {
        var template =
            new Dictionary<string, AttributeValue>(PrimitiveCollectionsItems.AttributeValues[1]);
        var item = new Dictionary<string, AttributeValue>(template)
        {
            ["Pk"] = new() { S = "ITEM#MISSING-OPTIONAL" },
        };
        item.Remove("OptionalTags");

        await PutItemAsync(item);

        var result =
            await Db.Items.FirstAsync(x => x.Pk == "ITEM#MISSING-OPTIONAL", CancellationToken);

        result.OptionalTags.Should().BeNull();
    }

    private Dictionary<string, AttributeValue> CreateValidTemplateItem(string pk)
    {
        var template =
            new Dictionary<string, AttributeValue>(PrimitiveCollectionsItems.AttributeValues[0]);
        var item = new Dictionary<string, AttributeValue>(template) { ["Pk"] = new() { S = pk } };

        return item;
    }

    /// <summary>Creates an item for the optional-collection test model.</summary>
    private static Dictionary<string, AttributeValue> CreateOptionalCollectionsItem(
        string pk,
        bool includeOptionalList,
        bool includeOptionalSet,
        bool includeOptionalDictionary,
        bool useDynamoNull)
    {
        var item = new Dictionary<string, AttributeValue> { ["Pk"] = new() { S = pk } };

        if (includeOptionalList)
            item["OptionalList"] = useDynamoNull
                ? new AttributeValue { NULL = true }
                : new AttributeValue { L = [new AttributeValue { S = "a" }] };

        if (includeOptionalSet)
            item["OptionalSet"] = useDynamoNull
                ? new AttributeValue { NULL = true }
                : new AttributeValue { SS = ["x", "y"] };

        if (includeOptionalDictionary)
            item["OptionalMap"] = useDynamoNull
                ? new AttributeValue { NULL = true }
                : new AttributeValue
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["k1"] = new() { N = "1" }, ["k2"] = new() { N = "2" },
                    },
                };

        return item;
    }

    /// <summary>Creates DbContext options for the optional collection test context.</summary>
    private DbContextOptions<OptionalCollectionsContext> CreateOptionalCollectionsOptions()
    {
        var builder = new DbContextOptionsBuilder<OptionalCollectionsContext>();
        builder.UseDynamo(options => options.ServiceUrl(ServiceUrl));
        return builder.Options;
    }

    private Task PutItemAsync(Dictionary<string, AttributeValue> item)
        => Client.PutItemAsync(
            new PutItemRequest
            {
                TableName = PrimitiveCollectionsDynamoFixture.TableName, Item = item,
            },
            CancellationToken);

    private sealed class OptionalCollectionsContext(
        DbContextOptions<OptionalCollectionsContext> options) : DbContext(options)
    {
        public DbSet<OptionalCollectionsItem> Items => Set<OptionalCollectionsItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OptionalCollectionsItem>(entity =>
            {
                entity.ToTable(PrimitiveCollectionsDynamoFixture.TableName);
                entity.HasKey(x => x.Pk);
            });
    }

    private sealed class OptionalCollectionsItem
    {
        public string Pk { get; set; } = default!;

        public List<string>? OptionalList { get; set; }

        public HashSet<string>? OptionalSet { get; set; }

        public Dictionary<string, int>? OptionalMap { get; set; }

        public string[]? OptionalArray { get; set; }
    }
}
