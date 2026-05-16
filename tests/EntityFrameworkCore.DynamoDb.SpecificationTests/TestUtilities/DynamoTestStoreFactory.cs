using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;

/// <summary>Test store factory for DynamoDB specification tests.</summary>
public class DynamoTestStoreFactory : ITestStoreFactory
{
    /// <summary>Gets the singleton DynamoDB test store factory.</summary>
    public static DynamoTestStoreFactory Instance { get; } = new();

    /// <summary>Gets the shared DynamoDB client from the active specification fixture.</summary>
    public IAmazonDynamoDB Client => DynamoSpecificationContainerFixture.Client;

    /// <summary>Adds DynamoDB provider services to the test service collection.</summary>
    public IServiceCollection AddProviderServices(IServiceCollection serviceCollection)
        => serviceCollection
            .AddEntityFrameworkDynamo()
            .AddSingleton<ILoggerFactory>(new TestSqlLoggerFactory());

    /// <summary>Creates a non-shared DynamoDB test store.</summary>
    public TestStore Create(string storeName) => new DynamoTestStore(storeName, false, this);

    /// <summary>Gets or creates a shared DynamoDB test store.</summary>
    public virtual TestStore GetOrCreate(string storeName)
        => new DynamoTestStore(storeName, true, this);

    /// <summary>Creates the list logger factory used to capture emitted PartiQL.</summary>
    public virtual ListLoggerFactory CreateListLoggerFactory(Func<string, bool> shouldLogCategory)
        => new TestSqlLoggerFactory(shouldLogCategory);

    internal Task<IAmazonDynamoDB> GetClientAsync()
        => DynamoSpecificationContainerFixture.GetClientAsync();
}
