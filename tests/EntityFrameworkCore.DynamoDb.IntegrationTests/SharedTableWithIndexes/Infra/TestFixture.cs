using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTableWithIndexes;

public abstract class SharedTableWithIndexesTestFixture : DynamoTestFixtureBase
{
    protected SharedTableWithIndexesTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(
            SharedTableWithIndexesItemTable.TableName,
            SharedTableWithIndexesItemTable.CreateTable);

    protected virtual DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.On;

    protected override bool UseSharedInternalServiceProvider => false;

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
