using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>
///     xUnit collection fixture that provisions a DynamoDB Local container for secondary-index
///     integration tests. Each test class that extends <see cref="SecondaryIndexTestBase" />
///     shares this container instance for the lifetime of the test run.
/// </summary>
public class SecondaryIndexDynamoFixture : DynamoFixture
{
    /// <summary>Physical DynamoDB table name used across all secondary-index integration tests.</summary>
    public const string TableName = "SecondaryIndexOrders";
}
