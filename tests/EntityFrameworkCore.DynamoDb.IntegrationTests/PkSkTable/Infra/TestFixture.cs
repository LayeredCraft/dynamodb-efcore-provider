using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

public abstract class PkSkTableTestFixture(DynamoContainerFixture fixture)
    : DynamoTestFixtureBase(fixture), IClassFixture<DynamoContainerFixture>
{
    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public PkSkTableDbContext Db
    {
        get
        {
            field ??= new PkSkTableDbContext(
                CreateOptions<PkSkTableDbContext>(options => options.DynamoDbClient(Client)));
            return field;
        }
    }
}
