using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ConstructorMaterializationTable;

public abstract class ConstructorMaterializationTableTestFixture : DynamoTestFixtureBase
{
    protected ConstructorMaterializationTableTestFixture(DynamoContainerFixture fixture) :
        base(fixture)
        => EnsureClassTableInitialized(
            ConstructorBlogTable.TableName,
            ConstructorBlogTable.CreateTable);

    public ConstructorMaterializationDbContext Db
    {
        get
        {
            field ??= new ConstructorMaterializationDbContext(
                CreateOptions<ConstructorMaterializationDbContext>(options
                    => options.DynamoDbClient(Client)));
            return field;
        }
    }

    protected async Task<Dictionary<string, AttributeValue>> GetBlogItemAsync(
        string pk,
        CancellationToken cancellationToken)
    {
        var response = await Client.GetItemAsync(
            new GetItemRequest
            {
                TableName = ConstructorBlogTable.TableName,
                Key = new Dictionary<string, AttributeValue> { ["pk"] = new() { S = pk } }
            },
            cancellationToken);

        return response.Item;
    }
}
