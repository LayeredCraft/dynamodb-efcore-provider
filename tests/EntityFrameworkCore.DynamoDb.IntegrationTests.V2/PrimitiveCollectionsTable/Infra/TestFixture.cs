using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.PrimitiveCollectionsTable;

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
