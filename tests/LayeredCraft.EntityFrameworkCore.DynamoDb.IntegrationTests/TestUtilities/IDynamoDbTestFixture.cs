using Amazon.DynamoDBv2;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;

public interface IDynamoDbTestFixture
{
    string ServiceUrl { get; }

    IAmazonDynamoDB Client { get; }
}
