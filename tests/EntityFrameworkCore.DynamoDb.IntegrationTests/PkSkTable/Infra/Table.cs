using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

public static class PkSkItemTable
{
    public const string TableName = "PkSkItems";

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
                ],
                KeySchema =
                [
                    new KeySchemaElement { AttributeName = "Pk", KeyType = KeyType.HASH },
                    new KeySchemaElement { AttributeName = "Sk", KeyType = KeyType.RANGE },
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            },
            cancellationToken);

        var writeRequests =
            PkSkItems
                .AttributeValues
                .Select(item => new WriteRequest { PutRequest = new PutRequest { Item = item } })
                .ToList();

        for (var i = 0; i < writeRequests.Count; i += 25)
        {
            var batch = writeRequests.Skip(i).Take(25).ToList();
            await BatchWriteWithRetriesAsync(dynamoDb, batch, cancellationToken);
        }
    }

    private static async Task BatchWriteWithRetriesAsync(
        IAmazonDynamoDB dynamoDb,
        List<WriteRequest> writeRequests,
        CancellationToken cancellationToken)
    {
        var request = new BatchWriteItemRequest
        {
            RequestItems =
                new Dictionary<string, List<WriteRequest>> { [TableName] = writeRequests },
        };

        while (request.RequestItems.Count > 0)
        {
            var response = await dynamoDb.BatchWriteItemAsync(request, cancellationToken);
            request.RequestItems = response.UnprocessedItems;
        }
    }
}
