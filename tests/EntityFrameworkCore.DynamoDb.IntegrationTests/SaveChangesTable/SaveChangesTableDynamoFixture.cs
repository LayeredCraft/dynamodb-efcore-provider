using EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>Represents the SaveChangesTableDynamoFixture type.</summary>
public sealed class SaveChangesTableDynamoFixture : DynamoFixture
{
    /// <summary>Provides functionality for this member.</summary>
    public const string TableName = "AppItems";
}
