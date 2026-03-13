using Amazon.DynamoDBv2;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

/// <summary>Represents the DynamoDbOptionsExtension type.</summary>
public class DynamoDbOptionsExtension : IDbContextOptionsExtension
{
    /// <summary>Provides functionality for this member.</summary>
    public IAmazonDynamoDB? DynamoDbClient { get; private set; }

    /// <summary>Provides functionality for this member.</summary>
    public AmazonDynamoDBConfig? DynamoDbClientConfig { get; private set; }

    /// <summary>Provides functionality for this member.</summary>
    public Action<AmazonDynamoDBConfig>? DynamoDbClientConfigAction { get; private set; }

    /// <summary>
    ///     The default number of items DynamoDB should evaluate per request. Null means no limit
    ///     (DynamoDB scans up to 1MB per request).
    /// </summary>
    public int? DefaultPageSize { get; private set; }

    /// <summary>Controls whether the provider should automatically select compatible secondary indexes.</summary>
    public DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode { get; private set; }

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

    /// <summary>Sets the default statement limit applied to query requests when not explicitly set.</summary>
    public virtual DynamoDbOptionsExtension WithDefaultPageSize(int? pageSize)
    {
        if (pageSize is <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be positive.");

        var clone = Clone();

        clone.DefaultPageSize = pageSize;

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

    /// <summary>Creates a copy of this extension with the current option values.</summary>
    protected virtual DynamoDbOptionsExtension Clone()
        => new()
        {
            DynamoDbClient = DynamoDbClient,
            DynamoDbClientConfig = DynamoDbClientConfig,
            DynamoDbClientConfigAction = DynamoDbClientConfigAction,
            DefaultPageSize = DefaultPageSize,
            AutomaticIndexSelectionMode = AutomaticIndexSelectionMode,
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
                hashCode.Add(Extension.DefaultPageSize);
                hashCode.Add(Extension.AutomaticIndexSelectionMode);

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
                && Extension.DefaultPageSize == otherInfo.Extension.DefaultPageSize
                && Extension.AutomaticIndexSelectionMode == otherInfo.Extension.AutomaticIndexSelectionMode;

        /// <summary>Provides functionality for this member.</summary>
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) { }

        /// <summary>Provides functionality for this member.</summary>
        public override bool IsDatabaseProvider => true;

        /// <summary>Provides functionality for this member.</summary>
        public override string LogFragment
        {
            get
            {
                field ??= $"DefaultPageSize={Extension.DefaultPageSize?.ToString() ?? "null"},"
                    + $"AutomaticIndexSelectionMode={Extension.AutomaticIndexSelectionMode},"
                    + $"DynamoDbClient={Extension.DynamoDbClient is not null},"
                    + $"DynamoDbClientConfig={Extension.DynamoDbClientConfig is not null},"
                    + $"DynamoDbClientConfigAction={Extension.DynamoDbClientConfigAction is not null}";
                return field;
            }
        }
    }
}
