using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.CompetingGsiTable;

public abstract class CompetingGsiTableTestFixture : DynamoTestFixtureBase
{
    protected CompetingGsiTableTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(
            CompetingGsiOrdersTable.TableName,
            CompetingGsiOrdersTable.CreateTable);

    protected virtual DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.Off;

    protected override bool UseSharedInternalServiceProvider => false;

    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public CompetingGsiDbContext Db
    {
        get
        {
            field ??= new CompetingGsiDbContext(
                CreateOptions<CompetingGsiDbContext>(options =>
                {
                    options.DynamoDbClient(Client);
                    options.UseAutomaticIndexSelection(AutomaticIndexSelectionMode);
                }));
            return field;
        }
    }
}
