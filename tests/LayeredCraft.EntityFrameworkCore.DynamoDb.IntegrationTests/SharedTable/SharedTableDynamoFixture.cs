using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable;

public sealed class SharedTableDynamoFixture : DynamoFixture
{
    public const string TableName = "app-table";
}
