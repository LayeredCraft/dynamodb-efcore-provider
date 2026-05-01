using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

public abstract class SecondaryIndexTableTestFixture : DynamoTestFixtureBase
{
    protected SecondaryIndexTableTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(
            SecondaryIndexOrdersTable.TableName,
            SecondaryIndexOrdersTable.CreateTable);

    protected virtual DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.On;

    protected override bool UseSharedInternalServiceProvider => false;

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
