using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class SimpleTableTestFixture : DynamoTestFixtureBase
{
    public SimpleTableTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(SimpleItemTable.TableName, SimpleItemTable.CreateTable);

    public SimpleTableDbContext Db
    {
        get
        {
            field ??= new SimpleTableDbContext(
                CreateOptions<SimpleTableDbContext>(o => o.DynamoDbClient(Client)));
            return field;
        }
    }
}
