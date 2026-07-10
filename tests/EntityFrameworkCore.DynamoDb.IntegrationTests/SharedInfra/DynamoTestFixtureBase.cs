using System.Collections.Concurrent;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

/// <summary>
///     Base class for all table-scoped test fixtures. Wires the integration-test
///     <see cref="TestPartiQlLoggerFactory" /> into every <see cref="DbContext" /> created via
///     <see cref="CreateOptions{T}" />, and exposes <see cref="AssertSql" /> so test classes can
///     assert emitted PartiQL without any additional setup.
/// </summary>
/// <remarks>
///     The SQL capture is shared with the shared internal service provider and cleared for each
///     test-class instance and after each <see cref="AssertSql" /> call. Integration tests disable
///     parallel execution at assembly level, so the shared capture is not read or cleared by
///     concurrent tests.
/// </remarks>
public abstract class DynamoTestFixtureBase
{
    private readonly DynamoContainerFixture _container;

    private static readonly TimeSpan TableLifecycleTimeout = TimeSpan.FromSeconds(30);

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> TableInitializationLocks =
        new(StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<(Type ClassType, string TableName), bool>
        InitializedClassTables = new();

    private static readonly TestPartiQlLoggerFactory SharedSqlCapture = new();

    private static readonly IServiceProvider SharedServiceProvider = new ServiceCollection()
        .AddLogging()
        .AddSingleton<ILoggerFactory>(SharedSqlCapture)
        .AddEntityFrameworkDynamo()
        .BuildServiceProvider();

    /// <summary>
    ///     The per-test SQL capture logger. Exposed for advanced assertions (pagination,
    ///     diagnostics).
    /// </summary>
    protected TestPartiQlLoggerFactory SqlCapture => SharedSqlCapture;

    /// <summary>The shared DynamoDB client backed by the Testcontainers instance.</summary>
    public IAmazonDynamoDB Client => _container.Client;

    /// <summary>Cancellation token for the currently executing test.</summary>
    protected CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    protected DynamoTestFixtureBase(DynamoContainerFixture container)
    {
        _container = container;
        SqlCapture.Clear();
    }

    protected virtual bool UseSharedInternalServiceProvider => true;

    /// <summary>
    ///     Builds <see cref="DbContextOptions{TContext}" /> with the DynamoDB provider configured and
    ///     the per-test SQL capture logger attached.
    /// </summary>
    /// <param name="configure">Additional provider options, e.g. setting the client.</param>
    protected DbContextOptions<T> CreateOptions<T>(Action<DynamoDbContextOptionsBuilder> configure)
        where T : DbContext
    {
        // UseDynamo/UseLoggerFactory return the non-generic DbContextOptionsBuilder, so we keep a
        // reference to the typed builder and read Options from it to preserve DbContextOptions<T>.
        var builder = new DbContextOptionsBuilder<T>();
        if (UseSharedInternalServiceProvider)
            builder.UseInternalServiceProvider(SharedServiceProvider);
        else
            builder.UseLoggerFactory(SqlCapture);

        builder.UseDynamo(configure);
        builder.ConfigureWarnings(w
            => w
                .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                .Ignore(DynamoEventId.ScanLikeQueryDetected));
        return builder.Options;
    }

    protected void EnsureClassTableInitialized(
        string tableName,
        Func<IAmazonDynamoDB, CancellationToken, Task> createTable)
    {
        var classTableKey = (GetType(), tableName);
        if (InitializedClassTables.ContainsKey(classTableKey))
            return;

        var gate = TableInitializationLocks.GetOrAdd(
            tableName,
            static _ => new SemaphoreSlim(1, 1));
        using var timeout = new CancellationTokenSource(TableLifecycleTimeout);
        gate.Wait(timeout.Token);

        try
        {
            if (InitializedClassTables.ContainsKey(classTableKey))
                return;

            RecreateTable(tableName, createTable, timeout.Token);
            InitializedClassTables[classTableKey] = true;
        }
        finally
        {
            gate.Release();
        }
    }

    private void RecreateTable(
        string tableName,
        Func<IAmazonDynamoDB, CancellationToken, Task> createTable,
        CancellationToken cancellationToken)
        => RecreateTableAsync(tableName, createTable, cancellationToken).GetAwaiter().GetResult();

    private async Task RecreateTableAsync(
        string tableName,
        Func<IAmazonDynamoDB, CancellationToken, Task> createTable,
        CancellationToken cancellationToken)
    {
        try
        {
            await Client.DeleteTableAsync(
                new DeleteTableRequest { TableName = tableName },
                cancellationToken);
            await WaitForTableDeletedAsync(tableName, cancellationToken);
        }
        catch (ResourceNotFoundException) { }

        await createTable(Client, cancellationToken);
        await WaitForTableActiveAsync(tableName, cancellationToken);
    }

    private async Task WaitForTableDeletedAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await Client.DescribeTableAsync(
                    new DescribeTableRequest { TableName = tableName },
                    cancellationToken);
            }
            catch (ResourceNotFoundException)
            {
                return;
            }
            catch (OperationCanceledException ex)
            {
                throw new TimeoutException(
                    $"Timed out waiting for DynamoDB table '{tableName}' to be deleted.",
                    ex);
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                throw new TimeoutException(
                    $"Timed out waiting for DynamoDB table '{tableName}' to be deleted.",
                    ex);
            }
        }
    }

    private async Task WaitForTableActiveAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                var response = await Client.DescribeTableAsync(
                    new DescribeTableRequest { TableName = tableName },
                    cancellationToken);

                var table = response.Table;
                if (table.TableStatus == TableStatus.ACTIVE
                    && (table.GlobalSecondaryIndexes is null
                        || table.GlobalSecondaryIndexes.All(i
                            => i.IndexStatus == IndexStatus.ACTIVE)))
                    return;
            }
            catch (ResourceNotFoundException) { }
            catch (OperationCanceledException ex)
            {
                throw new TimeoutException(
                    $"Timed out waiting for DynamoDB table '{tableName}' to become ACTIVE.",
                    ex);
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                throw new TimeoutException(
                    $"Timed out waiting for DynamoDB table '{tableName}' to become ACTIVE.",
                    ex);
            }
        }
    }

    /// <summary>
    ///     Asserts that the PartiQL statements emitted since the last assertion (or test start) match
    ///     <paramref name="expected" />, then clears the capture buffer.
    /// </summary>
    protected void AssertSql(params string[] expected) => SqlCapture.AssertBaseline(expected);

    protected async Task<T> GetItemAsync<T>(
        string tableName,
        Dictionary<string, AttributeValue> key,
        CancellationToken cancellationToken)
    {
        var mapper = _container.Mappers.Get<T>();

        var response = await Client.GetItemAsync(
            new GetItemRequest { TableName = tableName, Key = key },
            cancellationToken);

        return mapper.FromItem<T>(response.Item);
    }

    public async Task AssertItemExistsInDynamoDbAsync<T>(
        T item,
        string tableName,
        Dictionary<string, AttributeValue> key,
        CancellationToken cancellationToken = default)
        => (await GetItemAsync<T>(tableName, key, cancellationToken))
            .Should()
            .BeEquivalentTo(
                item,
                $"Item with key {key} does not match expected item in table {tableName}");
}
