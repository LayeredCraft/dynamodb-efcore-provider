using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public abstract class PrimitiveCollectionsTableTestFixture : DynamoTestFixtureBase
{
    protected PrimitiveCollectionsTableTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(
            PrimitiveCollectionsItemTable.TableName,
            PrimitiveCollectionsItemTable.CreateTable);

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
