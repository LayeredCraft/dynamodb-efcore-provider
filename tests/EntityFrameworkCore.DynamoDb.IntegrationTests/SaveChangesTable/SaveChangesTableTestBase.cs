using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>Represents the SaveChangesTableTestBase type.</summary>
public abstract class SaveChangesTableTestBase(SaveChangesTableDynamoFixture fixture)
    : DynamoDbPerTestResetTestBase<SaveChangesTableDynamoFixture, SaveChangesTableDbContext>(
        fixture)
{
    /// <summary>Creates the shared SaveChanges integration table with string PK and SK.</summary>
    protected override async Task CreateTablesAsync(CancellationToken cancellationToken)
    {
        await Client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = SaveChangesTableDynamoFixture.TableName,
                AttributeDefinitions =
                [
                    new AttributeDefinition
                    {
                        AttributeName = "Pk", AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "Sk", AttributeType = ScalarAttributeType.S,
                    },
                ],
                KeySchema =
                [
                    new KeySchemaElement { AttributeName = "Pk", KeyType = KeyType.HASH },
                    new KeySchemaElement { AttributeName = "Sk", KeyType = KeyType.RANGE },
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            },
            cancellationToken);

        await DynamoDbSchemaManager.WaitForTableActiveAsync(
            Client,
            SaveChangesTableDynamoFixture.TableName,
            cancellationToken);
    }

    /// <summary>Seeds deterministic shared-table rows for future SaveChanges scenarios.</summary>
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        var writeRequests =
            SaveChangesTableItems
                .AttributeValues
                .Select(item => new WriteRequest { PutRequest = new PutRequest { Item = item } })
                .ToList();

        for (var i = 0; i < writeRequests.Count; i += 25)
        {
            var batch = writeRequests.Skip(i).Take(25).ToList();
            await BatchWriteWithRetriesAsync(batch, cancellationToken);
        }
    }

    /// <summary>Writes a single item directly for test-specific setup.</summary>
    protected Task PutItemAsync(
        Dictionary<string, AttributeValue> item,
        CancellationToken cancellationToken)
        => Client.PutItemAsync(
            new PutItemRequest { TableName = SaveChangesTableDynamoFixture.TableName, Item = item },
            cancellationToken);

    /// <summary>Loads a raw item directly from DynamoDB by PK and SK.</summary>
    protected async Task<Dictionary<string, AttributeValue>?> GetItemAsync(
        string pk,
        string sk,
        CancellationToken cancellationToken)
    {
        var response = await Client.GetItemAsync(
            new GetItemRequest
            {
                TableName = SaveChangesTableDynamoFixture.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["Pk"] = new() { S = pk }, ["Sk"] = new() { S = sk },
                },
            },
            cancellationToken);

        // DynamoDB Local returns null (not an empty dict) for missing keys; handle both.
        return response.Item is { Count: > 0 } item ? item : null;
    }

    /// <summary>Writes a batch and retries unprocessed items until completion.</summary>
    private async Task BatchWriteWithRetriesAsync(
        List<WriteRequest> writeRequests,
        CancellationToken cancellationToken)
    {
        var request = new BatchWriteItemRequest
        {
            RequestItems = new Dictionary<string, List<WriteRequest>>
            {
                [SaveChangesTableDynamoFixture.TableName] = writeRequests,
            },
        };

        while (request.RequestItems.Count > 0)
        {
            var response = await Client.BatchWriteItemAsync(request, cancellationToken);
            request.RequestItems = response.UnprocessedItems;
        }
    }
}
