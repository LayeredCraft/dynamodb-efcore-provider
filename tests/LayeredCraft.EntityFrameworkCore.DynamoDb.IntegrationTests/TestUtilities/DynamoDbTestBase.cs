using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;

public abstract class DynamoDbTestBase<TFixture, TContext> : IClassFixture<TFixture>
    where TFixture : class, IDynamoDbTestFixture where TContext : DbContext
{
    protected DynamoDbTestBase(TFixture fixture) => Fixture = fixture;

    protected TFixture Fixture { get; }

    protected IAmazonDynamoDB Client => Fixture.Client;

    protected CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    protected virtual DbContextOptions<TContext> CreateOptions(
        TestPartiQlLoggerFactory loggerFactory)
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseDynamo(options => options.DynamoDbClient(Client));
        builder.UseLoggerFactory(loggerFactory);
        // Each test uses a fresh ILoggerFactory instance, which would cause EF Core to build a new
        // internal IServiceProvider per test and eventually trigger
        // ManyServiceProvidersCreatedWarning.
        // Disabling caching avoids that by opting out of the service provider pool entirely.
        builder.EnableServiceProviderCaching(false);

        return builder.Options;
    }

    protected virtual TContext CreateContext(DbContextOptions<TContext> options)
        => (TContext)Activator.CreateInstance(typeof(TContext), options)!;
}
