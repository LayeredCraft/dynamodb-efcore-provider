using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTableWithIndexes;

public static class SharedTableWithIndexesItemTable
{
    public const string TableName = "work-orders-indexed-table";

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
                        AttributeName = "pk", AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "sk", AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "status", AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName =
                            "priority",
                        AttributeType = ScalarAttributeType.N,
                    },
                ],
                KeySchema =
                [
                    new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
                    new KeySchemaElement { AttributeName = "sk", KeyType = KeyType.RANGE },
                ],
                GlobalSecondaryIndexes =
                [
                    new GlobalSecondaryIndex
                    {
                        IndexName = "ByPriority",
                        KeySchema =
                        [
                            new KeySchemaElement
                            {
                                AttributeName =
                                    "priority",
                                KeyType = KeyType.HASH,
                            },
                        ],
                        Projection =
                            new Projection { ProjectionType = ProjectionType.ALL },
                    },
                ],
                LocalSecondaryIndexes =
                [
                    new LocalSecondaryIndex
                    {
                        IndexName = "ByStatus",
                        KeySchema =
                        [
                            new KeySchemaElement
                            {
                                AttributeName = "pk", KeyType = KeyType.HASH,
                            },
                            new KeySchemaElement
                            {
                                AttributeName =
                                    "status",
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

        await dynamoDb.SeedItemsAsync(
            TableName,
            SharedTableWithIndexesItems.AttributeValues,
            cancellationToken);
    }
}
