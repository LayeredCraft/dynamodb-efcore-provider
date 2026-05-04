using System.Collections.Concurrent;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

/// <summary>
///     Base class for all table-scoped test fixtures. Wires a per-test
///     <see cref="TestPartiQlLoggerFactory" /> into every <see cref="DbContext" /> created via
///     <see cref="CreateOptions{T}" />, and exposes <see cref="AssertSql" /> so test classes can
///     assert emitted PartiQL without any additional setup.
/// </summary>
/// <remarks>
///     xUnit creates a new test-class instance per test method, so <see cref="SqlCapture" /> is
///     fresh for every test. The buffer is also cleared after each <see cref="AssertSql" /> call,
///     allowing multiple assertions within a single test.
/// </remarks>
public abstract class DynamoTestFixtureBase
{
    private readonly DynamoContainerFixture _container;

    private static readonly ConcurrentDictionary<Type, SemaphoreSlim> ClassInitializationLocks =
        new();

    private static readonly ConcurrentDictionary<Type, bool> InitializedClasses = new();

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
        var classType = GetType();
        if (InitializedClasses.ContainsKey(classType))
            return;

        var gate = ClassInitializationLocks.GetOrAdd(classType, _ => new SemaphoreSlim(1, 1));
        gate.Wait();

        try
        {
            if (InitializedClasses.ContainsKey(classType))
                return;

            RecreateTable(tableName, createTable);
            InitializedClasses[classType] = true;
        }
        finally
        {
            gate.Release();
        }
    }

    private void RecreateTable(
        string tableName,
        Func<IAmazonDynamoDB, CancellationToken, Task> createTable)
        => RecreateTableAsync(tableName, createTable, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

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

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
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

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    /// <summary>
    ///     Asserts that the PartiQL statements emitted since the last assertion (or test start) match
    ///     <paramref name="expected" />, then clears the capture buffer.
    /// </summary>
    protected void AssertSql(params string[] expected) => SqlCapture.AssertBaseline(expected);
}
