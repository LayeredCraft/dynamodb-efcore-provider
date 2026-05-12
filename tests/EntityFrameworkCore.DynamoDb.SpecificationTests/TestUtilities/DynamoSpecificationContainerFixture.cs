using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Testcontainers.DynamoDb;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;

/// <summary>Process-scoped DynamoDB Local container for specification tests.</summary>
public static class DynamoSpecificationContainerFixture
{
    private static readonly SemaphoreSlim StartLock = new(1, 1);
    private static DynamoDbContainer? _container;
    private static IAmazonDynamoDB? _client;

    /// <summary>Gets the shared DynamoDB client for specification tests.</summary>
    public static IAmazonDynamoDB Client
    {
        get
        {
            EnsureStartedAsync().GetAwaiter().GetResult();
            return _client!;
        }
    }

    private static async Task EnsureStartedAsync()
    {
        if (_client is not null)
            return;

        await StartLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_client is not null)
                return;

            _container = new DynamoDbBuilder("amazon/dynamodb-local:latest").Build();
            await _container.StartAsync().ConfigureAwait(false);

            _client = new AmazonDynamoDBClient(
                new BasicAWSCredentials("test", "test"),
                new AmazonDynamoDBConfig { ServiceURL = _container.GetConnectionString() });
        }
        finally
        {
            StartLock.Release();
        }
    }
}
