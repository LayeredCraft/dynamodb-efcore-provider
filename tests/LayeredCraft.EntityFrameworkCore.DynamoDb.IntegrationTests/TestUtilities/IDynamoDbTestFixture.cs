using Amazon.DynamoDBv2;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;

public interface IDynamoDbTestFixture
{
    IAmazonDynamoDB Client { get; }
}
