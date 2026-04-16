using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace Amazon.DynamoDBv2;

public static class AmazonDynamoDbExtensions
{
    extension(IAmazonDynamoDB client)
    {
        public async Task SeedItemsAsync(
            string tableName,
            IEnumerable<Dictionary<string, AttributeValue>> items,
            CancellationToken cancellationToken)
            => await items
                .Chunk(100)
                .ForEachAsync(async chunk => await client.TransactWriteItemsAsync(
                    new TransactWriteItemsRequest
                    {
                        TransactItems = chunk
                            .Select(a => new TransactWriteItem
                            {
                                Put = new Put { TableName = tableName, Item = a },
                            })
                            .ToList(),
                    },
                    cancellationToken));
    }
}
