using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SimpleTable;

public static class SimpleItemTable
{
    public const string TableName = "SimpleItem";

    public static async Task CreateTable(
        IAmazonDynamoDB dynamoDb,
        CancellationToken cancellationToken)
    {
        // create table
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
                ],
                KeySchema =
                [
                    new KeySchemaElement { AttributeName = "Pk", KeyType = KeyType.HASH },
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            },
            cancellationToken);

        // seed data
        await dynamoDb.TransactWriteItemsAsync(
            new TransactWriteItemsRequest
            {
                TransactItems =
                    SimpleItems
                        .AttributeValue
                        .Select(a => new TransactWriteItem
                        {
                            Put = new Put { TableName = TableName, Item = a },
                        })
                        .ToList(),
            },
            cancellationToken);
    }
}
