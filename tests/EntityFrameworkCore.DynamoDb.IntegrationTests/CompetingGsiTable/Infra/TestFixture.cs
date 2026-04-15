using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.CompetingGsiTable;

public abstract class CompetingGsiTableTestFixture(DynamoContainerFixture fixture)
    : DynamoTestFixtureBase(fixture), IClassFixture<DynamoContainerFixture>
{
    protected virtual DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.Off;

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
