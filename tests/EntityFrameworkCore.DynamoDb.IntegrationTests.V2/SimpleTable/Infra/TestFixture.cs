using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SimpleTable;

public class SimpleTableTestFixture(DynamoContainerFixture fixture)
    : DynamoTestFixtureBase(fixture), IClassFixture<DynamoContainerFixture>
{
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
