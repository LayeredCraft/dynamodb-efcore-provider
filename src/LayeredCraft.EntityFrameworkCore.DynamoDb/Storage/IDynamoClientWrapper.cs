using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;

public interface IDynamoClientWrapper
{
    IAmazonDynamoDB Client { get; }

    IAsyncEnumerable<Dictionary<string, AttributeValue>> ExecutePartiQl(
        ExecuteStatementRequest statementRequest);
}
