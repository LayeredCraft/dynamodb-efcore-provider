using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public class NullHandlingTests(PrimitiveCollectionsDynamoFixture fixture)
    : PrimitiveCollectionsTestBase(fixture)
{
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

    [Fact]
    public async Task Materialization_Throws_WhenNonNullableMapValueIsDynamoNull()
    {
        var item = CreateValidTemplateItem("ITEM#BAD-NULL-MAP-VALUE");
        item["ScoresByCategory"] = new AttributeValue
        {
            M = new Dictionary<string, AttributeValue>
            {
                ["math"] = new() { N = "10" }, ["science"] = new() { NULL = true },
            },
        };

        await PutItemAsync(item);

        var act = async ()
            => await Db
                .Items.Where(x => x.Pk == "ITEM#BAD-NULL-MAP-VALUE")
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*ScoresByCategory*")
            .WithMessage("*DynamoDB NULL*");
    }

    [Fact]
    public async Task Materialization_Throws_WhenNonNullableListElementIsDynamoNull()
    {
        await PutItemAsync(
            new Dictionary<string, AttributeValue>
            {
                ["Pk"] = new() { S = "ITEM#BAD-NULL-LIST-ELEMENT" },
                ["IntValues"] = new()
                {
                    L =
                    [
                        new AttributeValue { N = "1" },
                        new AttributeValue { NULL = true },
                        new AttributeValue { N = "2" },
                    ],
                },
            });

        var optionsBuilder = new DbContextOptionsBuilder<IntListDbContext>();
        optionsBuilder.UseDynamo(options => options.ServiceUrl(ServiceUrl));

        await using var db = new IntListDbContext(optionsBuilder.Options);

        var act = async ()
            => await db
                .Items.Where(x => x.Pk == "ITEM#BAD-NULL-LIST-ELEMENT")
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*IntValues*")
            .WithMessage("*DynamoDB NULL*");
    }

    [Fact]
    public async Task Materialization_AllowsDynamoNull_WhenListElementIsNullable()
    {
        await PutItemAsync(
            new Dictionary<string, AttributeValue>
            {
                ["Pk"] = new() { S = "ITEM#NULL-LIST-ELEMENT-ALLOWED" },
                ["NullableIntValues"] = new()
                {
                    L =
                    [
                        new AttributeValue { N = "1" },
                        new AttributeValue { NULL = true },
                        new AttributeValue { N = "2" },
                    ],
                },
            });

        var optionsBuilder = new DbContextOptionsBuilder<NullableIntListDbContext>();
        optionsBuilder.UseDynamo(options => options.ServiceUrl(ServiceUrl));

        await using var db = new NullableIntListDbContext(optionsBuilder.Options);

        var result = await db.Items.FirstAsync(
            x => x.Pk == "ITEM#NULL-LIST-ELEMENT-ALLOWED",
            CancellationToken);

        result.NullableIntValues.Should().Equal(1, null, 2);
    }

    private Dictionary<string, AttributeValue> CreateValidTemplateItem(string pk)
    {
        var template =
            new Dictionary<string, AttributeValue>(PrimitiveCollectionsItems.AttributeValues[0]);
        var item = new Dictionary<string, AttributeValue>(template) { ["Pk"] = new() { S = pk } };

        return item;
    }

    private Task PutItemAsync(Dictionary<string, AttributeValue> item)
        => Client.PutItemAsync(
            new PutItemRequest
            {
                TableName = PrimitiveCollectionsDynamoFixture.TableName, Item = item,
            },
            CancellationToken);

    private sealed class IntListDbContext(DbContextOptions<IntListDbContext> options) : DbContext(
        options)
    {
        public DbSet<IntListItem> Items => Set<IntListItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder
                .Entity<IntListItem>()
                .ToTable(PrimitiveCollectionsDynamoFixture.TableName)
                .HasKey(x => x.Pk);
    }

    private sealed class IntListItem
    {
        public string Pk { get; set; } = null!;

        public List<int> IntValues { get; set; } = [];
    }

    private sealed class NullableIntListDbContext(
        DbContextOptions<NullableIntListDbContext> options) : DbContext(options)
    {
        public DbSet<NullableIntListItem> Items => Set<NullableIntListItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder
                .Entity<NullableIntListItem>()
                .ToTable(PrimitiveCollectionsDynamoFixture.TableName)
                .HasKey(x => x.Pk);
    }

    private sealed class NullableIntListItem
    {
        public string Pk { get; set; } = null!;

        public List<int?> NullableIntValues { get; set; } = [];
    }
}
