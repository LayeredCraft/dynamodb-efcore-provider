using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SecondaryIndexProjectionTable;

public abstract class SecondaryIndexProjectionTableTestFixture(DynamoContainerFixture fixture)
    : DynamoTestFixtureBase(fixture), IClassFixture<DynamoContainerFixture>
{
    protected virtual DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.Off;

    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public SecondaryIndexProjectionDbContext Db
    {
        get
        {
            field ??= new SecondaryIndexProjectionDbContext(
                CreateOptions<SecondaryIndexProjectionDbContext>(options =>
                {
                    options.DynamoDbClient(Client);
                    options.UseAutomaticIndexSelection(AutomaticIndexSelectionMode);
                }));
            return field;
        }
    }
}
