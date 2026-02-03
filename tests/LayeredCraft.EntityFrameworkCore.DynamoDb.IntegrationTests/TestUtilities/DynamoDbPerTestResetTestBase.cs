using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;

public abstract class DynamoDbPerTestResetTestBase<TFixture, TContext>(TFixture fixture)
    : DynamoDbTestBase<TFixture, TContext>(fixture), IAsyncLifetime
    where TFixture : class, IDynamoDbTestFixture where TContext : DbContext
{
    private TestPartiQlLoggerFactory? _loggerFactory;
    private TContext? _db;

    protected TContext Db
        => _db
            ?? throw new InvalidOperationException(
                "DbContext is not initialized. Ensure the test class implements xUnit async lifetime correctly.");

    protected TestPartiQlLoggerFactory LoggerFactory
        => _loggerFactory
            ?? throw new InvalidOperationException("Logger factory is not initialized.");

    protected void AssertSql(params string[] expected) => LoggerFactory.AssertBaseline(expected);

    protected abstract Task CreateTablesAsync(CancellationToken cancellationToken);

    protected abstract Task SeedAsync(CancellationToken cancellationToken);

    public async ValueTask InitializeAsync()
    {
        await DynamoDbSchemaManager.DeleteAllTablesAsync(Client, CancellationToken);
        await CreateTablesAsync(CancellationToken);
        await SeedAsync(CancellationToken);

        _loggerFactory = new TestPartiQlLoggerFactory();
        var options = CreateOptions(_loggerFactory);
        _db = CreateContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        List<Exception>? exceptions = null;

        if (_db is not null)
            try
            {
                await _db.DisposeAsync();
            }
            catch (Exception ex)
            {
                exceptions ??= [];
                exceptions.Add(ex);
            }
            finally
            {
                _db = null;
            }

        try
        {
            _loggerFactory?.Clear();
            _loggerFactory?.Dispose();
        }
        catch (Exception ex)
        {
            exceptions ??= [];
            exceptions.Add(ex);
        }
        finally
        {
            _loggerFactory = null;
        }

        try
        {
            await DynamoDbSchemaManager.DeleteAllTablesAsync(Client, CancellationToken);
        }
        catch (Exception ex)
        {
            exceptions ??= [];
            exceptions.Add(ex);
        }

        if (exceptions is { Count: > 0 })
            throw exceptions.Count == 1 ? exceptions[0] : new AggregateException(exceptions);
    }
}
