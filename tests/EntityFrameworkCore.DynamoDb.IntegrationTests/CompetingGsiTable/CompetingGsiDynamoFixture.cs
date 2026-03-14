using EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.CompetingGsiTable;

/// <summary>xUnit fixture that provisions DynamoDB Local for competing-GSI integration tests.</summary>
public sealed class CompetingGsiDynamoFixture : DynamoFixture
{
    /// <summary>Physical table name used by competing-GSI integration tests.</summary>
    public const string TableName = "CompetingGsiOrders";
}
