using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>
///     Abstract base for all secondary-index integration tests. Creates the
///     <c>SecondaryIndexOrders</c> table with its full GSI and LSI configuration, seeds
///     deterministic data via batch write, and resets state between each test.
/// </summary>
public abstract class SecondaryIndexTestBase(SecondaryIndexDynamoFixture fixture)
    : DynamoDbPerTestResetTestBase<SecondaryIndexDynamoFixture, SecondaryIndexDbContext>(fixture)
{
    /// <inheritdoc />
    protected override async Task CreateTablesAsync(CancellationToken cancellationToken)
    {
        await Client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = SecondaryIndexDynamoFixture.TableName,

                // Only attributes that appear in at least one key schema need to be declared.
                AttributeDefinitions =
                [
                    // Base table keys.
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

                    // GSI "ByStatus" partition key.
                    new AttributeDefinition
                    {
                        AttributeName = nameof(OrderItem.Status),
                        AttributeType = ScalarAttributeType.S,
                    },

                    // Shared sort key for both GSIs and the ByCreatedAt LSI.
                    new AttributeDefinition
                    {
                        AttributeName = nameof(OrderItem.CreatedAt),
                        AttributeType = ScalarAttributeType.S,
                    },

                    // GSI "ByRegion" partition key.
                    new AttributeDefinition
                    {
                        AttributeName = nameof(OrderItem.Region),
                        AttributeType = ScalarAttributeType.S,
                    },

                    // LSI "ByPriority" sort key — numeric type.
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
                    // GSI: all orders for a given status, sorted by creation date.
                    new GlobalSecondaryIndex
                    {
                        IndexName = "ByStatus",
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
                        Projection = new Projection { ProjectionType = ProjectionType.ALL },
                    },

                    // GSI: all orders from a given region, sorted by creation date.
                    new GlobalSecondaryIndex
                    {
                        IndexName = "ByRegion",
                        KeySchema =
                        [
                            new KeySchemaElement
                            {
                                AttributeName = nameof(OrderItem.Region),
                                KeyType = KeyType.HASH,
                            },
                            new KeySchemaElement
                            {
                                AttributeName = nameof(OrderItem.CreatedAt),
                                KeyType = KeyType.RANGE,
                            },
                        ],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL },
                    },
                ],

                LocalSecondaryIndexes =
                [
                    // LSI: a customer's orders sorted by creation date (string sort key).
                    new LocalSecondaryIndex
                    {
                        IndexName = "ByCreatedAt",
                        KeySchema =
                        [
                            new KeySchemaElement
                            {
                                AttributeName = nameof(OrderItem.CustomerId),
                                KeyType = KeyType.HASH,
                            },
                            new KeySchemaElement
                            {
                                AttributeName = nameof(OrderItem.CreatedAt),
                                KeyType = KeyType.RANGE,
                            },
                        ],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL },
                    },

                    // LSI: a customer's orders sorted by numeric dispatch priority.
                    new LocalSecondaryIndex
                    {
                        IndexName = "ByPriority",
                        KeySchema =
                        [
                            new KeySchemaElement
                            {
                                AttributeName = nameof(OrderItem.CustomerId),
                                KeyType = KeyType.HASH,
                            },
                            new KeySchemaElement
                            {
                                AttributeName = nameof(OrderItem.Priority),
                                KeyType = KeyType.RANGE,
                            },
                        ],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL },
                    },
                ],

                BillingMode = BillingMode.PAY_PER_REQUEST,
            },
            cancellationToken);

        await DynamoDbSchemaManager.WaitForTableActiveAsync(
            Client,
            SecondaryIndexDynamoFixture.TableName,
            cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        var writeRequests =
            OrderItems
                .AttributeValues.Select(item
                    => new WriteRequest { PutRequest = new PutRequest { Item = item } })
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
                [SecondaryIndexDynamoFixture.TableName] = writeRequests,
            },
        };

        while (request.RequestItems.Count > 0)
        {
            var response = await Client.BatchWriteItemAsync(request, cancellationToken);
            request.RequestItems = response.UnprocessedItems;
        }
    }
}
