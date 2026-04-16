using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexProjectionTable;

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
                        AttributeName = "customerId",
                        AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "orderId",
                        AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "status", AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "createdAt",
                        AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "region", AttributeType = ScalarAttributeType.S,
                    },
                ],
                KeySchema =
                [
                    new KeySchemaElement
                    {
                        AttributeName = "customerId", KeyType = KeyType.HASH,
                    },
                    new KeySchemaElement
                    {
                        AttributeName = "orderId", KeyType = KeyType.RANGE,
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
                                AttributeName = "status",
                                KeyType = KeyType.HASH,
                            },
                            new KeySchemaElement
                            {
                                AttributeName = "createdAt",
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
                                AttributeName = "region",
                                KeyType = KeyType.HASH,
                            },
                            new KeySchemaElement
                            {
                                AttributeName = "createdAt",
                                KeyType = KeyType.RANGE,
                            },
                        ],
                        Projection =
                            new Projection
                            {
                                ProjectionType = ProjectionType.INCLUDE,
                                NonKeyAttributes = ["status"],
                            },
                    },
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            },
            cancellationToken);

        await dynamoDb.SeedItemsAsync(TableName, OrderItems.AttributeValues, cancellationToken);
    }
}
