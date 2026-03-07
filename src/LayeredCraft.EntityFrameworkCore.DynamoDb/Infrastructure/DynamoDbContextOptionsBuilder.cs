using Amazon.DynamoDBv2;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure;

public interface IDynamoDbContextOptionsBuilder
{
    DbContextOptionsBuilder OptionsBuilder { get; }
}

public class DynamoDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
    : IDynamoDbContextOptionsBuilder
{
    public DbContextOptionsBuilder OptionsBuilder { get; } = optionsBuilder;

    /// <summary>Uses a preconfigured DynamoDB client instance for all provider operations.</summary>
    /// <param name="client">The DynamoDB client instance.</param>
    /// <returns>The builder for chaining.</returns>
    public virtual DynamoDbContextOptionsBuilder DynamoDbClient(IAmazonDynamoDB client)
        => WithOption(e => e.WithDynamoDbClient(client.NotNull()));

    /// <summary>Uses the provided DynamoDB client configuration when creating the SDK client.</summary>
    /// <param name="config">The DynamoDB client configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public virtual DynamoDbContextOptionsBuilder DynamoDbClientConfig(AmazonDynamoDBConfig config)
        => WithOption(e => e.WithDynamoDbClientConfig(config.NotNull()));

    /// <summary>
    ///     Applies additional configuration to the DynamoDB client configuration before client
    ///     creation.
    /// </summary>
    /// <param name="configure">The callback used to configure the client configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public virtual DynamoDbContextOptionsBuilder ConfigureDynamoDbClientConfig(
        Action<AmazonDynamoDBConfig> configure)
        => WithOption(e => e.WithDynamoDbClientConfigAction(configure.NotNull()));

    /// <summary>
    ///     Sets the default number of items DynamoDB should evaluate per request. Null means no limit
    ///     (DynamoDB scans up to 1MB per request).
    /// </summary>
    /// <param name="pageSize">The default page size. Must be positive if specified.</param>
    /// <returns>The builder for chaining.</returns>
    public virtual DynamoDbContextOptionsBuilder DefaultPageSize(int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be positive.");

        return WithOption(e => e.WithDefaultPageSize(pageSize));
    }

    /// <summary>Updates the provider options extension with the supplied mutation action.</summary>
    protected virtual DynamoDbContextOptionsBuilder WithOption(
        Func<DynamoDbOptionsExtension, DynamoDbOptionsExtension> setAction)
    {
        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(
            setAction(
                OptionsBuilder.Options.FindExtension<DynamoDbOptionsExtension>()
                ?? new DynamoDbOptionsExtension()));

        return this;
    }
}
