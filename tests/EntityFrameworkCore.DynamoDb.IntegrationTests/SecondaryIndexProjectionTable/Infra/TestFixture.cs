using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexProjectionTable;

public abstract class SecondaryIndexProjectionTableTestFixture : DynamoTestFixtureBase
{
    protected SecondaryIndexProjectionTableTestFixture(DynamoContainerFixture fixture) :
        base(fixture)
        => EnsureClassTableInitialized(
            SecondaryIndexProjectionOrdersTable.TableName,
            SecondaryIndexProjectionOrdersTable.CreateTable);

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
