using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

/// <summary>Represents the PrimitiveCollectionsTestBase type.</summary>
public abstract class PrimitiveCollectionsTestBase(PrimitiveCollectionsDynamoFixture fixture)
    : DynamoDbPerTestResetTestBase<PrimitiveCollectionsDynamoFixture,
        PrimitiveCollectionsDbContext>(fixture)
{
    /// <summary>Provides functionality for this member.</summary>
    protected override async Task CreateTablesAsync(CancellationToken cancellationToken)
    {
        await Client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = PrimitiveCollectionsDynamoFixture.TableName,
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
            PrimitiveCollectionsDynamoFixture.TableName,
            cancellationToken);
    }

    /// <summary>Provides functionality for this member.</summary>
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        var writeRequests =
            PrimitiveCollectionsItems
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
                [PrimitiveCollectionsDynamoFixture.TableName] = writeRequests,
            },
        };

        while (request.RequestItems.Count > 0)
        {
            var response = await Client.BatchWriteItemAsync(request, cancellationToken);
            request.RequestItems = response.UnprocessedItems;
        }
    }
}
