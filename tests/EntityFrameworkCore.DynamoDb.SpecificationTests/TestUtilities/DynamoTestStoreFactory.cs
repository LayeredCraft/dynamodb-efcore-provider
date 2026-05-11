using Amazon.DynamoDBv2;
using Amazon.Runtime;
using EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.DynamoDb;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;

/// <summary>Test store factory for DynamoDB specification tests.</summary>
public class DynamoTestStoreFactory : ITestStoreFactory
{
    private readonly SemaphoreSlim _containerLock = new(1, 1);
    private DynamoDbContainer? _container;
    private IAmazonDynamoDB? _client;

    public IAmazonDynamoDB Client
    {
        get
        {
            EnsureStartedAsync().GetAwaiter().GetResult();
            return _client!;
        }
    }

    public IServiceCollection AddProviderServices(IServiceCollection serviceCollection)
        => serviceCollection
            .AddEntityFrameworkDynamo()
            .AddSingleton<ILoggerFactory>(new TestSqlLoggerFactory());

    public TestStore Create(string storeName) => new DynamoTestStore(storeName, false, this);

    public virtual TestStore GetOrCreate(string storeName)
        => new DynamoTestStore(storeName, true, this);

    public virtual ListLoggerFactory CreateListLoggerFactory(Func<string, bool> shouldLogCategory)
        => new TestSqlLoggerFactory(shouldLogCategory);

    internal async Task<IAmazonDynamoDB> GetClientAsync()
    {
        await EnsureStartedAsync().ConfigureAwait(false);
        return _client!;
    }

    private async Task EnsureStartedAsync()
    {
        if (_client is not null)
            return;

        await _containerLock.WaitAsync().ConfigureAwait(false);
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
            _containerLock.Release();
        }
    }
}
