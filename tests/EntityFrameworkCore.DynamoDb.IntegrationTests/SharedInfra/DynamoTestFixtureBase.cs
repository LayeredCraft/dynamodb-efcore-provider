using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

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
public abstract class DynamoTestFixtureBase(DynamoContainerFixture container)
{
    /// <summary>
    ///     The per-test SQL capture logger. Exposed for advanced assertions (pagination,
    ///     diagnostics).
    /// </summary>
    protected TestPartiQlLoggerFactory SqlCapture { get; } = new();

    /// <summary>The shared DynamoDB client backed by the Testcontainers instance.</summary>
    public IAmazonDynamoDB Client => container.Client;

    /// <summary>Cancellation token for the currently executing test.</summary>
    protected CancellationToken CancellationToken => TestContext.Current.CancellationToken;

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
        builder.UseDynamo(configure);
        builder.UseLoggerFactory(SqlCapture);
        builder.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        return builder.Options;
    }

    /// <summary>
    ///     Asserts that the PartiQL statements emitted since the last assertion (or test start) match
    ///     <paramref name="expected" />, then clears the capture buffer.
    /// </summary>
    protected void AssertSql(params string[] expected) => SqlCapture.AssertBaseline(expected);
}
