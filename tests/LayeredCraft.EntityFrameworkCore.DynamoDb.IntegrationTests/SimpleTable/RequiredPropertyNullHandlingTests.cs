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
            .WithMessage("*SimpleItem*IntValue*")
            .WithMessage("*IntValue*")
            .WithMessage("*not present*");
    }

    [Fact]
    public async Task Materialization_Throws_WhenRequiredReferencePropertyIsDynamoNull()
    {
        var item = CreateValidTemplateItem("ITEM#BAD-NULL-STRING");
        item["StringValue"] = new AttributeValue { NULL = true };

        await PutItemAsync(item);

        var act = async ()
            => await Db
                .SimpleItems.Where(x => x.Pk == "ITEM#BAD-NULL-STRING")
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*StringValue*")
            .WithMessage("*DynamoDB NULL*");
    }

    [Fact]
    public async Task Materialization_Throws_WhenRequiredBoolPropertyIsDynamoNull()
    {
        var item = CreateValidTemplateItem("ITEM#BAD-NULL-BOOL");
        item["BoolValue"] = new AttributeValue { NULL = true };

        await PutItemAsync(item);

        var act = async ()
            => await Db
                .SimpleItems.Where(x => x.Pk == "ITEM#BAD-NULL-BOOL")
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*BoolValue*")
            .WithMessage("*DynamoDB NULL*");
    }

    [Fact]
    public async Task Materialization_Throws_WhenRequiredNumericWireMemberIsMissing()
    {
        var item = CreateValidTemplateItem("ITEM#BAD-WIRE-MISSING-N");
        item["IntValue"] = new AttributeValue { S = "123" };

        await PutItemAsync(item);

        var act = async ()
            => await Db
                .SimpleItems.Where(x => x.Pk == "ITEM#BAD-WIRE-MISSING-N")
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*IntValue*")
            .WithMessage("*wire member*")
            .WithMessage("*'N'*");
    }

    [Fact]
    public async Task Materialization_Throws_WhenRequiredBoolWireMemberIsMissing()
    {
        var item = CreateValidTemplateItem("ITEM#BAD-WIRE-MISSING-BOOL");
        item["BoolValue"] = new AttributeValue { S = "true" };

        await PutItemAsync(item);

        var act = async ()
            => await Db
                .SimpleItems.Where(x => x.Pk == "ITEM#BAD-WIRE-MISSING-BOOL")
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*BoolValue*")
            .WithMessage("*wire member*")
            .WithMessage("*'BOOL'*");
    }

    [Fact]
    public async Task Materialization_ReturnsNull_WhenNullablePropertyIsMissing()
    {
        var item = CreateValidTemplateItem("ITEM#OPTIONAL-MISSING-INT");
        item.Remove("NullableIntValue");

        await PutItemAsync(item);

        var results =
            await Db
                .SimpleItems.Where(x => x.Pk == "ITEM#OPTIONAL-MISSING-INT")
                .ToListAsync(CancellationToken);

        results.Should().HaveCount(1);
        results[0].NullableIntValue.Should().BeNull();
    }

    [Fact]
    public async Task Materialization_ReturnsNull_WhenNullablePropertyIsDynamoNull()
    {
        var item = CreateValidTemplateItem("ITEM#OPTIONAL-NULL-INT");
        item["NullableIntValue"] = new AttributeValue { NULL = true };

        await PutItemAsync(item);

        var results =
            await Db
                .SimpleItems.Where(x => x.Pk == "ITEM#OPTIONAL-NULL-INT")
                .ToListAsync(CancellationToken);

        results.Should().HaveCount(1);
        results[0].NullableIntValue.Should().BeNull();
    }

    [Fact]
    public async Task ScalarProjection_Throws_WhenNonNullableValueTypeProjectionIsMissing()
    {
        var item = CreateValidTemplateItem("ITEM#PROJECTION-MISSING-INT");
        item.Remove("IntValue");

        await PutItemAsync(item);

        var act = async ()
            => await Db
                .SimpleItems.Where(x => x.Pk == "ITEM#PROJECTION-MISSING-INT")
                .Select(x => x.IntValue)
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*IntValue*")
            .WithMessage("*not present*");
    }

    private static Dictionary<string, AttributeValue> CreateValidTemplateItem(string pk)
    {
        var template = new Dictionary<string, AttributeValue>(SimpleItems.AttributeValues[0]);
        template["Pk"] = new AttributeValue { S = pk };
        return template;
    }

    private Task PutItemAsync(Dictionary<string, AttributeValue> item)
        => Client.PutItemAsync(
            new PutItemRequest { TableName = SimpleTableDynamoFixture.TableName, Item = item },
            CancellationToken);
}
