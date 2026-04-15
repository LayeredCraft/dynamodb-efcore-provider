using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedTable.SharedTableWithIndexes;

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
                        AttributeName = "Pk", AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "Sk", AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "Status", AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName =
                            "Priority",
                        AttributeType = ScalarAttributeType.N,
                    },
                ],
                KeySchema =
                [
                    new KeySchemaElement { AttributeName = "Pk", KeyType = KeyType.HASH },
                    new KeySchemaElement { AttributeName = "Sk", KeyType = KeyType.RANGE },
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
                                    "Priority",
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
                                AttributeName = "Pk", KeyType = KeyType.HASH,
                            },
                            new KeySchemaElement
                            {
                                AttributeName =
                                    "Status",
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

        foreach (var chunk in SharedTableWithIndexesItems.AttributeValues.Chunk(100))
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
