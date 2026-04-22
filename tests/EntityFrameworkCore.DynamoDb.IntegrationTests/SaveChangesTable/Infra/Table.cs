using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

public static class SaveChangesItemTable
{
    public const string TableName = "AppItems";

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
                    new AttributeDefinition { AttributeName = "pk", AttributeType = ScalarAttributeType.S },
                    new AttributeDefinition { AttributeName = "sk", AttributeType = ScalarAttributeType.S },
                    new AttributeDefinition { AttributeName = "gs1-pk", AttributeType = ScalarAttributeType.S },
                    new AttributeDefinition { AttributeName = "gs1-sk", AttributeType = ScalarAttributeType.S },
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
                        IndexName = "gs1-index",
                        KeySchema =
                        [
                            new KeySchemaElement { AttributeName = "gs1-pk", KeyType = KeyType.HASH },
                            new KeySchemaElement { AttributeName = "gs1-sk", KeyType = KeyType.RANGE },
                        ],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL },
                    },
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            },
            cancellationToken);

        foreach (var chunk in SaveChangesTableItems.AttributeValues.Chunk(100))
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
