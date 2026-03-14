using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.CompetingGsiTable;

/// <summary>Base class that provisions and seeds the competing-GSI table per test.</summary>
public abstract class CompetingGsiTestBase(CompetingGsiDynamoFixture fixture)
    : DynamoDbPerTestResetTestBase<CompetingGsiDynamoFixture, CompetingGsiDbContext>(fixture)
{
    /// <inheritdoc />
    protected override async Task CreateTablesAsync(CancellationToken cancellationToken)
    {
        await Client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = CompetingGsiDynamoFixture.TableName,
                AttributeDefinitions =
                [
                    new AttributeDefinition
                    {
                        AttributeName = nameof(OrderItem.CustomerId),
                        AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = nameof(OrderItem.OrderId),
                        AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = nameof(OrderItem.Status),
                        AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = nameof(OrderItem.CreatedAt),
                        AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = nameof(OrderItem.Priority),
                        AttributeType = ScalarAttributeType.N,
                    },
                ],
                KeySchema =
                [
                    new KeySchemaElement
                    {
                        AttributeName = nameof(OrderItem.CustomerId),
                        KeyType = KeyType.HASH,
                    },
                    new KeySchemaElement
                    {
                        AttributeName = nameof(OrderItem.OrderId),
                        KeyType = KeyType.RANGE,
                    },
                ],
                GlobalSecondaryIndexes =
                [
                    new GlobalSecondaryIndex
                    {
                        IndexName = "ByStatusCreatedAt",
                        KeySchema =
                        [
                            new KeySchemaElement
                            {
                                AttributeName = nameof(OrderItem.Status),
                                KeyType = KeyType.HASH,
                            },
                            new KeySchemaElement
                            {
                                AttributeName = nameof(OrderItem.CreatedAt),
                                KeyType = KeyType.RANGE,
                            },
                        ],
                        Projection =
                            new Projection { ProjectionType = ProjectionType.ALL },
                    },
                    new GlobalSecondaryIndex
                    {
                        IndexName = "ByStatusPriority",
                        KeySchema =
                        [
                            new KeySchemaElement
                            {
                                AttributeName = nameof(OrderItem.Status),
                                KeyType = KeyType.HASH,
                            },
                            new KeySchemaElement
                            {
                                AttributeName = nameof(OrderItem.Priority),
                                KeyType = KeyType.RANGE,
                            },
                        ],
                        Projection =
                            new Projection { ProjectionType = ProjectionType.ALL },
                    },
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            },
            cancellationToken);

        await DynamoDbSchemaManager.WaitForTableActiveAsync(
            Client,
            CompetingGsiDynamoFixture.TableName,
            cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        var writeRequests =
            OrderItems
                .AttributeValues
                .Select(item => new WriteRequest { PutRequest = new PutRequest { Item = item } })
                .ToList();

        for (var i = 0; i < writeRequests.Count; i += 25)
        {
            var batch = writeRequests.Skip(i).Take(25).ToList();
            await BatchWriteWithRetriesAsync(batch, cancellationToken);
        }
    }

    /// <summary>Retries batch writes until DynamoDB Local reports no unprocessed items.</summary>
    private async Task BatchWriteWithRetriesAsync(
        List<WriteRequest> writeRequests,
        CancellationToken cancellationToken)
    {
        var request = new BatchWriteItemRequest
        {
            RequestItems = new Dictionary<string, List<WriteRequest>>
            {
                [CompetingGsiDynamoFixture.TableName] = writeRequests,
            },
        };

        while (request.RequestItems.Count > 0)
        {
            var response = await Client.BatchWriteItemAsync(request, cancellationToken);
            request.RequestItems = response.UnprocessedItems;
        }
    }
}
