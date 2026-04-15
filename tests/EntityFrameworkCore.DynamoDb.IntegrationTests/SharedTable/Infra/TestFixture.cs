using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable;

/// <summary>
///     Test fixture base for shared-table scenarios. Exposes the default two-type context as
///     <see cref="DbContext" />; use <see cref="DynamoTestFixtureBase.CreateOptions{T}" /> directly
///     for the other context variants.
/// </summary>
public class SharedTableTestFixture(DynamoContainerFixture fixture)
    : DynamoTestFixtureBase(fixture), IClassFixture<DynamoContainerFixture>
{
    /// <summary>A fresh <see cref="SharedTableDbContext" /> wired to the per-test SQL capture logger.</summary>
    public SharedTableDbContext Db
        => new(CreateOptions<SharedTableDbContext>(o => o.DynamoDbClient(Client)));
}
