using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public abstract class PrimitiveCollectionsTableTestFixture(DynamoContainerFixture fixture)
    : DynamoTestFixtureBase(fixture), IClassFixture<DynamoContainerFixture>
{
    public PrimitiveCollectionsDbContext Db
    {
        get
        {
            field ??= new PrimitiveCollectionsDbContext(
                CreateOptions<PrimitiveCollectionsDbContext>(options
                    => options.DynamoDbClient(Client)));
            return field;
        }
    }
}
