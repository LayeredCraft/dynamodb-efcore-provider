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
}
