using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedTable;

/// <summary>
///     Test fixture base for shared-table scenarios. Wraps <see cref="DynamoContainerFixture" />
///     and provides a typed <see cref="DbContext" /> via a caller-supplied factory delegate.
/// </summary>
public class SharedTableTestFixture<TContext>(
    DynamoContainerFixture fixture,
    Func<IAmazonDynamoDB, TContext> contextFactory) : IClassFixture<DynamoContainerFixture>
    where TContext : DbContext
{
    public IAmazonDynamoDB Client => fixture.Client;

    public TContext DbContext => contextFactory(fixture.Client);
}
