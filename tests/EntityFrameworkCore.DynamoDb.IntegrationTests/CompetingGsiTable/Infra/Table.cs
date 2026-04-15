using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.CompetingGsiTable;

public static class CompetingGsiOrdersTable
{
    public const string TableName = "CompetingGsiOrders";

    public static async Task CreateTable(
        IAmazonDynamoDB dynamoDb,
        CancellationToken cancellationToken)
    {
        await dynamoDb.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = TableName,
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

        foreach (var chunk in OrderItems.AttributeValues.Chunk(100))
            await dynamoDb.TransactWriteItemsAsync(
                new TransactWriteItemsRequest
                {
                    TransactItems =
                        chunk
                            .Select(attributes => new TransactWriteItem
                            {
                                Put = new Put { TableName = TableName, Item = attributes },
                            })
                            .ToList(),
                },
                cancellationToken);
    }
}
