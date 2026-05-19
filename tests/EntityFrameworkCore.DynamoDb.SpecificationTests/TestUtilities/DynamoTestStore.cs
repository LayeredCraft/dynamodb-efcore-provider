using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;

/// <summary>Test store for DynamoDB specification tests.</summary>
public class DynamoTestStore(string name, bool shared, DynamoTestStoreFactory factory) : TestStore(
    name,
    shared)
{
    private IAmazonDynamoDB Client => factory.Client;

    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => builder.UseDynamo(options => options.DynamoDbClient(Client));

    /// <inheritdoc />
    public override async Task CleanAsync(DbContext context)
    {
        context.ChangeTracker.Clear();

        await context.Database.EnsureDeletedAsync().ConfigureAwait(false);
        await context.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }

    protected override async Task InitializeAsync(
        Func<DbContext> createContext,
        Func<DbContext, Task>? seed,
        Func<DbContext, Task>? clean)
    {
        await factory.GetClientAsync().ConfigureAwait(false);
        await base.InitializeAsync(createContext, seed, clean).ConfigureAwait(false);
    }
}
