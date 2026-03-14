using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable;

/// <summary>Represents the SharedTableTestBase type.</summary>
public abstract class SharedTableTestBase<TContext>(SharedTableDynamoFixture fixture)
    : DynamoDbPerTestResetTestBase<SharedTableDynamoFixture, TContext>(fixture)
    where TContext : DbContext
{
    /// <summary>Provides functionality for this member.</summary>
    protected override async Task CreateTablesAsync(CancellationToken cancellationToken)
    {
        await Client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = SharedTableDynamoFixture.TableName,
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
            SharedTableDynamoFixture.TableName,
            cancellationToken);
    }

    /// <summary>Provides functionality for this member.</summary>
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        var writeRequests =
            SharedTableItems
                .AttributeValues
                .Select(item => new WriteRequest { PutRequest = new PutRequest { Item = item } })
                .ToList();

        for (var i = 0; i < writeRequests.Count; i += 25)
        {
            var batch = writeRequests.Skip(i).Take(25).ToList();
            await BatchWriteWithRetriesAsync(batch, cancellationToken);
        }
    }

    private async Task BatchWriteWithRetriesAsync(
        List<WriteRequest> writeRequests,
        CancellationToken cancellationToken)
    {
        var request = new BatchWriteItemRequest
        {
            RequestItems = new Dictionary<string, List<WriteRequest>>
            {
                [SharedTableDynamoFixture.TableName] = writeRequests,
            },
        };

        while (request.RequestItems.Count > 0)
        {
            var response = await Client.BatchWriteItemAsync(request, cancellationToken);
            request.RequestItems = response.UnprocessedItems;
        }
    }
}
