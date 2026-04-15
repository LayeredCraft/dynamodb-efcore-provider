using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

public abstract class PkSkTableTestFixture : DynamoTestFixtureBase
{
    protected PkSkTableTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(PkSkItemTable.TableName, PkSkItemTable.CreateTable);

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
