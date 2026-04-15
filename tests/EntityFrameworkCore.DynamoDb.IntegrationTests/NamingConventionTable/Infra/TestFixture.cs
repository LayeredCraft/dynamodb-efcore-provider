using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventionTable;

/// <summary>
///     Test fixture for naming convention integration tests. Creates the DynamoDB table once per
///     test class and provides a lazily-initialized <see cref="Db" /> context.
/// </summary>
public class NamingConventionTableTestFixture : DynamoTestFixtureBase
{
    /// <summary>Provides functionality for this member.</summary>
    public NamingConventionTableTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(
            NamingConventionItemTable.TableName,
            NamingConventionItemTable.CreateTable);

    /// <summary>Lazily-initialized DbContext for the naming convention table.</summary>
    public NamingConventionTableDbContext Db
    {
        get
        {
            field ??= new NamingConventionTableDbContext(
                CreateOptions<NamingConventionTableDbContext>(o => o.DynamoDbClient(Client)));
            return field;
        }
    }
}
