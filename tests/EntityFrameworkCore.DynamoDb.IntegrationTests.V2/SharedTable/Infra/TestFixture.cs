using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedTable;

/// <summary>
///     Test fixture base for shared-table scenarios. Wraps <see cref="DynamoContainerFixture" />
///     and provides a typed <see cref="DbContext" /> via a caller-supplied factory delegate.
/// </summary>
public class SharedTableTestFixture(DynamoContainerFixture fixture)
    : IClassFixture<DynamoContainerFixture>
{
    public IAmazonDynamoDB Client => fixture.Client;

    public SharedTableDbContext DbContext => SharedTableDbContext.Create(fixture.Client);
}
