using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

public abstract class SaveChangesTableTestFixture(DynamoContainerFixture fixture)
    : DynamoTestFixtureBase(fixture), IClassFixture<DynamoContainerFixture>
{
    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public SaveChangesTableDbContext Db
    {
        get
        {
            field ??= new SaveChangesTableDbContext(
                CreateOptions<SaveChangesTableDbContext>(o => o.DynamoDbClient(Client)));
            return field;
        }
    }

    protected Task PutItemAsync(
        Dictionary<string, AttributeValue> item,
        CancellationToken cancellationToken)
        => Client.PutItemAsync(
            new PutItemRequest { TableName = SaveChangesItemTable.TableName, Item = item },
            cancellationToken);

    protected async Task<Dictionary<string, AttributeValue>?> GetItemAsync(
        string pk,
        string sk,
        CancellationToken cancellationToken)
    {
        var response = await Client.GetItemAsync(
            new GetItemRequest
            {
                TableName = SaveChangesItemTable.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["Pk"] = new() { S = pk }, ["Sk"] = new() { S = sk },
                },
            },
            cancellationToken);

        return response.Item is { Count: > 0 } item ? item : null;
    }

    protected async Task BumpVersionAsync(string pk, string sk, CancellationToken ct)
    {
        var item = await GetItemAsync(pk, sk, ct)
            ?? throw new InvalidOperationException(
                $"Cannot bump version: item {pk}/{sk} not found.");

        var currentVersion = long.Parse(item["Version"].N);
        item["Version"] = new AttributeValue { N = (currentVersion + 1).ToString() };
        await PutItemAsync(item, ct);
    }
}
