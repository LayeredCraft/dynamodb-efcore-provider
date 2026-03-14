using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable;

/// <summary>Represents the SharedTableDynamoFixture type.</summary>
public sealed class SharedTableDynamoFixture : DynamoFixture
{
    /// <summary>Provides functionality for this member.</summary>
    public const string TableName = "app-table";
}
