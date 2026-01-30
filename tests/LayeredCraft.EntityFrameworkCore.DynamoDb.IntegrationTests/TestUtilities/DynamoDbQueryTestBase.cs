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
        bool sensitiveDataLogging = false,
        TestPartiQlLoggerFactory? loggerFactory = null)
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseDynamo(options => options.ServiceUrl(ServiceUrl));
        builder.UseLoggerFactory(loggerFactory ?? SqlLoggerFactory);

        if (sensitiveDataLogging)
            builder.EnableSensitiveDataLogging();

        return builder.Options;
    }

    protected virtual TContext CreateContext(DbContextOptions<TContext> options)
        => (TContext)Activator.CreateInstance(typeof(TContext), options)!;

    protected TContext CreateContext(bool sensitiveDataLogging = false)
        => CreateContext(CreateOptions(sensitiveDataLogging));

    protected void AssertSql(params string[] expected) => SqlLoggerFactory.AssertBaseline(expected);

    protected async Task AssertSqlSensitiveAsync(
        Func<TContext, Task> execute,
        params string[] expected)
    {
        var loggerFactory = new TestPartiQlLoggerFactory();
        await using var context = CreateContext(CreateOptions(true, loggerFactory));
        await execute(context);
        loggerFactory.AssertBaseline(expected);
    }
}
