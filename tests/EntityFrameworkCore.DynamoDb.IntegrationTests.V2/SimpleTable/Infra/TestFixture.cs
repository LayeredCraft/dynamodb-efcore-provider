using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SimpleTable;

public class SimpleTableTestFixture(DynamoContainerFixture fixture)
    : IClassFixture<DynamoContainerFixture>
{
    public IAmazonDynamoDB Client => fixture.Client;

    public SimpleTableDbContext DbContext => SimpleTableDbContext.Create(fixture.Client);
}
