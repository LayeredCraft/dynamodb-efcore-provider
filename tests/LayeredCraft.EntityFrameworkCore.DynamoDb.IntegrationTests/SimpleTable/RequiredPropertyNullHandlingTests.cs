using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class RequiredPropertyNullHandlingTests(SimpleTableDynamoFixture fixture)
    : SimpleTableTestBase(fixture)
{
    [Fact]
    public async Task Materialization_Throws_WhenRequiredPropertyIsDynamoNull()
    {
        var template = new Dictionary<string, AttributeValue>(SimpleItems.AttributeValues[0]);
        var item = new Dictionary<string, AttributeValue>(template)
        {
            ["Pk"] = new() { S = "ITEM#BAD-NULL-INT" }, ["IntValue"] = new() { NULL = true },
        };

        await Client.PutItemAsync(
            new PutItemRequest { TableName = SimpleTableDynamoFixture.TableName, Item = item },
            CancellationToken);

        var act = async ()
            => await Db
                .SimpleItems.Where(x => x.Pk == "ITEM#BAD-NULL-INT")
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*IntValue*")
            .WithMessage("*DynamoDB NULL*");
    }

    [Fact]
    public async Task Materialization_Throws_WhenRequiredPropertyIsMissing()
    {
        var template = new Dictionary<string, AttributeValue>(SimpleItems.AttributeValues[0]);
        var item = new Dictionary<string, AttributeValue>(template)
        {
            ["Pk"] = new() { S = "ITEM#BAD-MISSING-INT" },
        };
        item.Remove("IntValue");

        await Client.PutItemAsync(
            new PutItemRequest { TableName = SimpleTableDynamoFixture.TableName, Item = item },
            CancellationToken);

        var act = async ()
            => await Db
                .SimpleItems.Where(x => x.Pk == "ITEM#BAD-MISSING-INT")
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*IntValue*")
            .WithMessage("*not present*");
    }
}
