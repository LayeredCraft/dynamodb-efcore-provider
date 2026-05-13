using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;

/// <summary>Test store factory for DynamoDB specification tests.</summary>
public class DynamoTestStoreFactory : ITestStoreFactory
{
    public static DynamoTestStoreFactory Instance { get; } = new();

    public IAmazonDynamoDB Client => DynamoSpecificationContainerFixture.Client;

    public IServiceCollection AddProviderServices(IServiceCollection serviceCollection)
        => serviceCollection
            .AddEntityFrameworkDynamo()
            .AddSingleton<ILoggerFactory>(new TestSqlLoggerFactory());

    public TestStore Create(string storeName) => new DynamoTestStore(storeName, false, this);

    public virtual TestStore GetOrCreate(string storeName)
        => new DynamoTestStore(storeName, true, this);

    public virtual ListLoggerFactory CreateListLoggerFactory(Func<string, bool> shouldLogCategory)
        => new TestSqlLoggerFactory(shouldLogCategory);

    internal Task<IAmazonDynamoDB> GetClientAsync() => Task.FromResult(Client);
}
