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

    /// <summary>Configures how the provider should apply automatic secondary index selection.</summary>
    /// <returns>The builder for chaining.</returns>
    public virtual DynamoDbContextOptionsBuilder UseAutomaticIndexSelection(
        DynamoAutomaticIndexSelectionMode mode)
        => WithOption(e => e.WithAutomaticIndexSelectionMode(mode));

    /// <summary>
    /// Configures how transactional SaveChanges should behave when the write unit exceeds max
    /// transaction size.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public virtual DynamoDbContextOptionsBuilder TransactionOverflowBehavior(
        TransactionOverflowBehavior behavior)
        => WithOption(e => e.WithTransactionOverflowBehavior(behavior));

    /// <summary>
    /// Configures the maximum number of write operations sent in a single DynamoDB transaction.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public virtual DynamoDbContextOptionsBuilder MaxTransactionSize(int maxTransactionSize)
        => WithOption(e => e.WithMaxTransactionSize(maxTransactionSize));

    /// <summary>
    ///     Configures default maximum number of write operations sent in a single non-atomic PartiQL
    ///     batch.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public virtual DynamoDbContextOptionsBuilder MaxBatchWriteSize(int maxBatchWriteSize)
        => WithOption(e => e.WithMaxBatchWriteSize(maxBatchWriteSize));

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
