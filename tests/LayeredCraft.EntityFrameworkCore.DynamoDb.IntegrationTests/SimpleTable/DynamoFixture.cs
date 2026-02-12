using Amazon.DynamoDBv2;
using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;
using Testcontainers.DynamoDb;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class DynamoFixture : IAsyncLifetime, IDynamoDbTestFixture
{
    public IAmazonDynamoDB Client
    {
        get
        {
            field ??= new AmazonDynamoDBClient(
                new AmazonDynamoDBConfig { ServiceURL = Container.GetConnectionString() });
            return field;
        }
    }

    public DynamoDbContainer Container { get; }

    public DynamoFixture()
        => Container =
            new DynamoDbBuilder("amazon/dynamodb-local:latest").Build()
            ?? throw new Exception("Failed to create DynamoDB Container");

    public virtual async ValueTask InitializeAsync() => await Container.StartAsync();

    public virtual async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

public class SimpleTableDynamoFixture : DynamoFixture
{
    public const string TableName = "SimpleItems";
}
