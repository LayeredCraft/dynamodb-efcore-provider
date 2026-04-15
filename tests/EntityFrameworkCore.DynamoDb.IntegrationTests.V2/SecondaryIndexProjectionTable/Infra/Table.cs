using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SecondaryIndexTable;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SecondaryIndexProjectionTable;

public static class SecondaryIndexProjectionOrdersTable
{
    public const string TableName = "SecondaryIndexProjectionOrders";

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
                        AttributeName = nameof(OrderItem.Region),
                        AttributeType = ScalarAttributeType.S,
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
                        IndexName = "ByStatusKeysOnly",
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
                            new Projection
                            {
                                ProjectionType = ProjectionType.KEYS_ONLY,
                            },
                    },
                    new GlobalSecondaryIndex
                    {
                        IndexName = "ByRegionInclude",
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
                        Projection =
                            new Projection
                            {
                                ProjectionType = ProjectionType.INCLUDE,
                                NonKeyAttributes = [nameof(OrderItem.Status)],
                            },
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
                            .Select(item => new TransactWriteItem
                            {
                                Put = new Put { TableName = TableName, Item = item },
                            })
                            .ToList(),
                },
                cancellationToken);
    }
}
