using Amazon.DynamoDBv2;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;

/// <summary>Defines the contract for IDynamoDbTestFixture.</summary>
public interface IDynamoDbTestFixture
{
    /// <summary>Provides functionality for this member.</summary>
    IAmazonDynamoDB Client { get; }
}
