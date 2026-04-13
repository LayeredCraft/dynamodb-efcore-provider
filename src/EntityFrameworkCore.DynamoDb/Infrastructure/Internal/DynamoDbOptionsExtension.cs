using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

/// <summary>Represents the DynamoDbOptionsExtension type.</summary>
public class DynamoDbOptionsExtension : IDbContextOptionsExtension
{
    private const int DynamoTransactionLimit = 100;
    private const int DynamoPartiQlBatchLimit = 25;

    /// <summary>Provides functionality for this member.</summary>
    public IAmazonDynamoDB? DynamoDbClient { get; private set; }

    /// <summary>Provides functionality for this member.</summary>
    public AmazonDynamoDBConfig? DynamoDbClientConfig { get; private set; }

    /// <summary>Provides functionality for this member.</summary>
    public Action<AmazonDynamoDBConfig>? DynamoDbClientConfigAction { get; private set; }

    /// <summary>Controls whether the provider should automatically select compatible secondary indexes.</summary>
    public DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode { get; private set; }

    /// <summary>
    /// Controls how SaveChanges behaves when a transactional write unit exceeds max transaction size.
    /// </summary>
    public TransactionOverflowBehavior TransactionOverflowBehavior { get; private set; }

    /// <summary>
    /// Maximum number of write operations sent in a single DynamoDB transaction.
    /// </summary>
    public int MaxTransactionSize { get; private set; } = DynamoTransactionLimit;

    /// <summary>Maximum number of write operations sent in a single non-atomic PartiQL batch.</summary>
    public int MaxBatchWriteSize { get; private set; } = DynamoPartiQlBatchLimit;

    /// <summary>Registers provider services in the EF Core internal service container.</summary>
    public virtual void ApplyServices(IServiceCollection services)
        => services.AddEntityFrameworkDynamo();

    /// <summary>Validates configured options for this provider extension.</summary>
    public void Validate(IDbContextOptions options) { }

    /// <summary>Provides functionality for this member.</summary>
    public DbContextOptionsExtensionInfo Info
    {
        get
        {
            field ??= new DynamoOptionsExtensionInfo(this);
            return field;
        }
    }

    /// <summary>Sets a preconfigured DynamoDB client instance for provider operations.</summary>
    public virtual DynamoDbOptionsExtension WithDynamoDbClient(IAmazonDynamoDB? client)
    {
        var clone = Clone();

        clone.DynamoDbClient = client;

        return clone;
    }

    /// <summary>Sets the base SDK configuration used when creating the DynamoDB client.</summary>
    public virtual DynamoDbOptionsExtension WithDynamoDbClientConfig(AmazonDynamoDBConfig? config)
    {
        var clone = Clone();

        clone.DynamoDbClientConfig = config;

        return clone;
    }

    /// <summary>Sets a callback that configures SDK client configuration before client creation.</summary>
    public virtual DynamoDbOptionsExtension WithDynamoDbClientConfigAction(
        Action<AmazonDynamoDBConfig>? configure)
    {
        var clone = Clone();

        clone.DynamoDbClientConfigAction = configure;

        return clone;
    }

    /// <summary>Sets how the provider should apply automatic secondary index selection.</summary>
    /// <returns>A cloned options extension containing the updated mode.</returns>
    public virtual DynamoDbOptionsExtension WithAutomaticIndexSelectionMode(
        DynamoAutomaticIndexSelectionMode mode)
    {
        var clone = Clone();

        clone.AutomaticIndexSelectionMode = mode;

        return clone;
    }

    /// <summary>
    /// Sets how transactional SaveChanges should behave when one transaction cannot represent
    /// the full write unit.
    /// </summary>
    public virtual DynamoDbOptionsExtension WithTransactionOverflowBehavior(
        TransactionOverflowBehavior behavior)
    {
        var clone = Clone();

        clone.TransactionOverflowBehavior = behavior;

        return clone;
    }

    /// <summary>
    /// Sets the maximum number of write operations sent in a single DynamoDB transaction.
    /// </summary>
    public virtual DynamoDbOptionsExtension WithMaxTransactionSize(int maxTransactionSize)
    {
        if (maxTransactionSize is <= 0 or > DynamoTransactionLimit)
            throw new InvalidOperationException(
                $"The specified 'MaxTransactionSize' value '{maxTransactionSize}' is not valid. "
                + $"It must be between 1 and {DynamoTransactionLimit}.");

        var clone = Clone();

        clone.MaxTransactionSize = maxTransactionSize;

        return clone;
    }

    /// <summary>Sets the maximum number of write operations sent in a single non-atomic PartiQL batch.</summary>
    public virtual DynamoDbOptionsExtension WithMaxBatchWriteSize(int maxBatchWriteSize)
    {
        if (maxBatchWriteSize is <= 0 or > DynamoPartiQlBatchLimit)
            throw new InvalidOperationException(
                $"The specified 'MaxBatchWriteSize' value '{maxBatchWriteSize}' is not valid. "
                + $"It must be between 1 and {DynamoPartiQlBatchLimit}.");

        var clone = Clone();

        clone.MaxBatchWriteSize = maxBatchWriteSize;

        return clone;
    }

    /// <summary>Creates a copy of this extension with the current option values.</summary>
    protected virtual DynamoDbOptionsExtension Clone()
        => new()
        {
            DynamoDbClient = DynamoDbClient,
            DynamoDbClientConfig = DynamoDbClientConfig,
            DynamoDbClientConfigAction = DynamoDbClientConfigAction,
            AutomaticIndexSelectionMode = AutomaticIndexSelectionMode,
            TransactionOverflowBehavior = TransactionOverflowBehavior,
            MaxTransactionSize = MaxTransactionSize,
            MaxBatchWriteSize = MaxBatchWriteSize,
        };

    /// <summary>Represents the DynamoOptionsExtensionInfo type.</summary>
    public class DynamoOptionsExtensionInfo(IDbContextOptionsExtension extension)
        : DbContextOptionsExtensionInfo(extension)
    {
        private int? _serviceProviderHash;

        private new DynamoDbOptionsExtension Extension => (DynamoDbOptionsExtension)base.Extension;

        /// <summary>Provides functionality for this member.</summary>
        public override int GetServiceProviderHashCode()
        {
            if (_serviceProviderHash == null)
            {
                var hashCode = new HashCode();

                hashCode.Add(Extension.DynamoDbClient);
                hashCode.Add(Extension.DynamoDbClientConfig);
                hashCode.Add(Extension.DynamoDbClientConfigAction);
                hashCode.Add(Extension.AutomaticIndexSelectionMode);
                hashCode.Add(Extension.TransactionOverflowBehavior);
                hashCode.Add(Extension.MaxTransactionSize);
                hashCode.Add(Extension.MaxBatchWriteSize);

                _serviceProviderHash = hashCode.ToHashCode();
            }

            return _serviceProviderHash.Value;
        }

        /// <summary>Provides functionality for this member.</summary>
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is DynamoOptionsExtensionInfo otherInfo
                && ReferenceEquals(Extension.DynamoDbClient, otherInfo.Extension.DynamoDbClient)
                && ReferenceEquals(
                    Extension.DynamoDbClientConfig,
                    otherInfo.Extension.DynamoDbClientConfig)
                && ReferenceEquals(
                    Extension.DynamoDbClientConfigAction,
                    otherInfo.Extension.DynamoDbClientConfigAction)
                && Extension.AutomaticIndexSelectionMode
                == otherInfo.Extension.AutomaticIndexSelectionMode
                && Extension.TransactionOverflowBehavior
                == otherInfo.Extension.TransactionOverflowBehavior
                && Extension.MaxTransactionSize == otherInfo.Extension.MaxTransactionSize
                && Extension.MaxBatchWriteSize == otherInfo.Extension.MaxBatchWriteSize;

        /// <summary>Provides functionality for this member.</summary>
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) { }

        /// <summary>Provides functionality for this member.</summary>
        public override bool IsDatabaseProvider => true;

        /// <summary>Provides functionality for this member.</summary>
        public override string LogFragment
        {
            get
            {
                field ??= $"AutomaticIndexSelectionMode={Extension.AutomaticIndexSelectionMode},"
                    + $"TransactionOverflowBehavior={Extension.TransactionOverflowBehavior},"
                    + $"MaxTransactionSize={Extension.MaxTransactionSize},"
                    + $"MaxBatchWriteSize={Extension.MaxBatchWriteSize},"
                    + $"DynamoDbClient={Extension.DynamoDbClient is not null},"
                    + $"DynamoDbClientConfig={Extension.DynamoDbClientConfig is not null},"
                    + $"DynamoDbClientConfigAction={Extension.DynamoDbClientConfigAction is not null}";
                return field;
            }
        }
    }
}
