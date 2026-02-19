using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

public abstract class OwnedTypesTableTestBase(OwnedTypesTableDynamoFixture fixture)
    : DynamoDbPerTestResetTestBase<OwnedTypesTableDynamoFixture, OwnedTypesTableDbContext>(fixture)
{
    /// <summary>Creates the owned-types integration table with a string partition key.</summary>
    protected override async Task CreateTablesAsync(CancellationToken cancellationToken)
    {
        await Client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = OwnedTypesTableDynamoFixture.TableName,
                AttributeDefinitions =
                [
                    new AttributeDefinition
                    {
                        AttributeName = "Pk", AttributeType = ScalarAttributeType.S,
                    },
                ],
                KeySchema =
                [
                    new KeySchemaElement { AttributeName = "Pk", KeyType = KeyType.HASH },
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            },
            cancellationToken);

        await DynamoDbSchemaManager.WaitForTableActiveAsync(
            Client,
            OwnedTypesTableDynamoFixture.TableName,
            cancellationToken);
    }

    /// <summary>Seeds deterministic owned-types payloads into the table.</summary>
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        var writeRequests =
            OwnedTypesItems
                .AttributeValues.Select(item
                    => new WriteRequest { PutRequest = new PutRequest { Item = item } })
                .ToList();

        for (var i = 0; i < writeRequests.Count; i += 25)
        {
            var batch = writeRequests.Skip(i).Take(25).ToList();
            await BatchWriteWithRetriesAsync(batch, cancellationToken);
        }
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
                [OwnedTypesTableDynamoFixture.TableName] = writeRequests,
            },
        };

        while (request.RequestItems.Count > 0)
        {
            var response = await Client.BatchWriteItemAsync(request, cancellationToken);
            request.RequestItems = response.UnprocessedItems;
        }
    }

    /// <summary>Writes a single DynamoDB item for scenario-specific test setup.</summary>
    protected Task PutItemAsync(
        Dictionary<string, AttributeValue> item,
        CancellationToken cancellationToken)
        => Client.PutItemAsync(
            new PutItemRequest { TableName = OwnedTypesTableDynamoFixture.TableName, Item = item },
            cancellationToken);
}
