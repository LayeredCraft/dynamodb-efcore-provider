using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SecondaryIndexTable;

public abstract class SecondaryIndexTableTestFixture(DynamoContainerFixture fixture)
    : DynamoTestFixtureBase(fixture), IClassFixture<DynamoContainerFixture>
{
    protected virtual DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.Off;

    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public SecondaryIndexDbContext Db
    {
        get
        {
            field ??= new SecondaryIndexDbContext(
                CreateOptions<SecondaryIndexDbContext>(options =>
                {
                    options.DynamoDbClient(Client);
                    options.UseAutomaticIndexSelection(AutomaticIndexSelectionMode);
                }));
            return field;
        }
    }
}
