using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexProjectionTable;

/// <summary>xUnit fixture that provisions DynamoDB Local for projection-type index integration tests.</summary>
public sealed class SecondaryIndexProjectionDynamoFixture : DynamoFixture
{
    /// <summary>Physical table name used by projection-type index integration tests.</summary>
    public const string TableName = "SecondaryIndexProjectionOrders";
}
