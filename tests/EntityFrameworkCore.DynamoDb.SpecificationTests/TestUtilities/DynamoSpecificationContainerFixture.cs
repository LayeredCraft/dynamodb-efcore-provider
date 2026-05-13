using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Testcontainers.DynamoDb;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;

/// <summary>Collection fixture that owns the shared DynamoDB Local container.</summary>
public sealed class DynamoSpecificationContainerFixture : IAsyncLifetime
{
    private const string DynamoDbLocalImage = "amazon/dynamodb-local:2.6.1";

    private static readonly SemaphoreSlim StartLock = new(1, 1);
    private static DynamoDbContainer? _container;
    private static AmazonDynamoDBClient? _client;

    /// <summary>Gets the shared DynamoDB client for specification tests.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the fixture has not been initialized.</exception>
    public static IAmazonDynamoDB Client
        => _client
            ?? throw new InvalidOperationException(
                "DynamoDB specification fixture has not been initialized.");

    /// <summary>Starts the shared DynamoDB Local container if needed.</summary>
    public static async Task<IAmazonDynamoDB> GetClientAsync()
    {
        if (_client is not null)
            return _client;

        await StartLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_client is not null)
                return _client;

            _container = new DynamoDbBuilder(DynamoDbLocalImage).Build();
            await _container.StartAsync().ConfigureAwait(false);

            _client = new AmazonDynamoDBClient(
                new BasicAWSCredentials("test", "test"),
                new AmazonDynamoDBConfig { ServiceURL = _container.GetConnectionString() });

            return _client;
        }
        finally
        {
            StartLock.Release();
        }
    }

    /// <summary>Starts the shared DynamoDB Local container.</summary>
    public async Task InitializeAsync() => await GetClientAsync().ConfigureAwait(false);

    /// <summary>Stops the shared DynamoDB Local container and disposes the client.</summary>
    public static async Task DisposeContainerAsync()
    {
        _client?.Dispose();
        _client = null;

        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
            _container = null;
        }
    }

    /// <summary>Stops the shared DynamoDB Local container and disposes the client.</summary>
    public async Task DisposeAsync() => await DisposeContainerAsync().ConfigureAwait(false);
}

/// <summary>Collection definition for DynamoDB specification tests.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DynamoSpecificationCollection
    : ICollectionFixture<DynamoSpecificationContainerFixture>
{
    /// <summary>Collection name for DynamoDB specification tests.</summary>
    public const string Name = "DynamoDB specification tests";
}
