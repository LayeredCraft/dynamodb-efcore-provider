using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.Infrastructure;

/// <summary>Defines the contract for IDynamoDbContextOptionsBuilder.</summary>
public interface IDynamoDbContextOptionsBuilder
{
    /// <summary>Provides functionality for this member.</summary>
    DbContextOptionsBuilder OptionsBuilder { get; }
}

/// <summary>Represents the DynamoDbContextOptionsBuilder type.</summary>
public class DynamoDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
    : IDynamoDbContextOptionsBuilder
{
    /// <summary>Provides functionality for this member.</summary>
    public DbContextOptionsBuilder OptionsBuilder { get; } = optionsBuilder;

    /// <summary>Uses a preconfigured DynamoDB client instance for all provider operations.</summary>
    /// <returns>The builder for chaining.</returns>
    public virtual DynamoDbContextOptionsBuilder DynamoDbClient(IAmazonDynamoDB client)
        => WithOption(e => e.WithDynamoDbClient(client.NotNull()));

    /// <summary>Uses the provided DynamoDB client configuration when creating the SDK client.</summary>
    /// <returns>The builder for chaining.</returns>
    public virtual DynamoDbContextOptionsBuilder DynamoDbClientConfig(AmazonDynamoDBConfig config)
        => WithOption(e => e.WithDynamoDbClientConfig(config.NotNull()));

    /// <summary>
    ///     Applies additional configuration to the DynamoDB client configuration before client
    ///     creation.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public virtual DynamoDbContextOptionsBuilder ConfigureDynamoDbClientConfig(
        Action<AmazonDynamoDBConfig> configure)
        => WithOption(e => e.WithDynamoDbClientConfigAction(configure.NotNull()));

    /// <summary>
    ///     Sets the default number of items DynamoDB should evaluate per request. Null means no limit
    ///     (DynamoDB scans up to 1MB per request).
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public virtual DynamoDbContextOptionsBuilder DefaultPageSize(int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be positive.");

        return WithOption(e => e.WithDefaultPageSize(pageSize));
    }

    /// <summary>Configures how the provider should apply automatic secondary index selection.</summary>
    /// <returns>The builder for chaining.</returns>
    public virtual DynamoDbContextOptionsBuilder UseAutomaticIndexSelection(
        DynamoAutomaticIndexSelectionMode mode)
        => WithOption(e => e.WithAutomaticIndexSelectionMode(mode));

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
