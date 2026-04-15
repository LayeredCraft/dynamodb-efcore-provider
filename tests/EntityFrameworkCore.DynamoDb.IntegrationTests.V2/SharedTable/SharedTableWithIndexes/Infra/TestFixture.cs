using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedTable.SharedTableWithIndexes;

public abstract class SharedTableWithIndexesTestFixture(DynamoContainerFixture fixture)
    : DynamoTestFixtureBase(fixture), IClassFixture<DynamoContainerFixture>
{
    protected virtual DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.Conservative;

    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public SharedTableWithIndexesDbContext Db
    {
        get
        {
            field ??= CreateDbContext(AutomaticIndexSelectionMode);
            return field;
        }
    }

    protected SharedTableWithIndexesDbContext CreateDbContext(
        DynamoAutomaticIndexSelectionMode automaticIndexSelectionMode)
        => new(
            CreateOptions<SharedTableWithIndexesDbContext>(options =>
            {
                options.DynamoDbClient(Client);
                options.UseAutomaticIndexSelection(automaticIndexSelectionMode);
            }));
}
