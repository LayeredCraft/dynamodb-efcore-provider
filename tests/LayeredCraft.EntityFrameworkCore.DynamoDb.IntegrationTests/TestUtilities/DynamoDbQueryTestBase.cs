using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;

public abstract class DynamoDbQueryTestBase<TFixture, TContext> : IClassFixture<TFixture>
    where TFixture : class where TContext : DbContext
{
    protected DynamoDbQueryTestBase(TFixture fixture, string serviceUrl)
    {
        Fixture = fixture;
        ServiceUrl = serviceUrl;
    }

    protected TFixture Fixture { get; }

    protected string ServiceUrl { get; }

    protected CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    protected TestPartiQlLoggerFactory SqlLoggerFactory { get; } = new();

    protected virtual DbContextOptions<TContext> CreateOptions(
        TestPartiQlLoggerFactory? loggerFactory = null)
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseDynamo(options => options.ServiceUrl(ServiceUrl));
        builder.UseLoggerFactory(loggerFactory ?? SqlLoggerFactory);

        return builder.Options;
    }

    protected virtual TContext CreateContext(DbContextOptions<TContext> options)
        => (TContext)Activator.CreateInstance(typeof(TContext), options)!;

    protected TContext CreateContext() => CreateContext(CreateOptions());

    protected void AssertSql(params string[] expected) => SqlLoggerFactory.AssertBaseline(expected);

    // Add more helpers here as tests grow (AssertContainsSql, AssertNoSql, etc.)
}
