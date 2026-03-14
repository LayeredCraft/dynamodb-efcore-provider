using EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

/// <summary>Represents the OwnedTypesTableDynamoFixture type.</summary>
public class OwnedTypesTableDynamoFixture : DynamoFixture
{
    /// <summary>Provides functionality for this member.</summary>
    public const string TableName = "OwnedTypesItems";
}
