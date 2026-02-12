using Amazon.DynamoDBv2;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

public class DynamoDbOptionsExtension : IDbContextOptionsExtension
{
    public IAmazonDynamoDB? DynamoDbClient { get; private set; }
    public AmazonDynamoDBConfig? DynamoDbClientConfig { get; private set; }
    public Action<AmazonDynamoDBConfig>? DynamoDbClientConfigAction { get; private set; }

    /// <summary>
    ///     The default number of items DynamoDB should evaluate per request. Null means no limit
    ///     (DynamoDB scans up to 1MB per request).
    /// </summary>
    public int? DefaultPageSize { get; private set; }

    /// <summary>Registers provider services in the EF Core internal service container.</summary>
    public virtual void ApplyServices(IServiceCollection services)
        => services.AddEntityFrameworkDynamo();

    /// <summary>Validates configured options for this provider extension.</summary>
    public void Validate(IDbContextOptions options) { }

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

    /// <summary>Creates a copy of this extension with the current option values.</summary>
    protected virtual DynamoDbOptionsExtension Clone()
        => new()
        {
            DynamoDbClient = DynamoDbClient,
            DynamoDbClientConfig = DynamoDbClientConfig,
            DynamoDbClientConfigAction = DynamoDbClientConfigAction,
            DefaultPageSize = DefaultPageSize,
        };

    public class DynamoOptionsExtensionInfo(IDbContextOptionsExtension extension)
        : DbContextOptionsExtensionInfo(extension)
    {
        private int? _serviceProviderHash;

        private new DynamoDbOptionsExtension Extension => (DynamoDbOptionsExtension)base.Extension;

        public override int GetServiceProviderHashCode()
        {
            if (_serviceProviderHash == null)
            {
                var hashCode = new HashCode();

                hashCode.Add(Extension.DynamoDbClient);
                hashCode.Add(Extension.DynamoDbClientConfig);
                hashCode.Add(Extension.DynamoDbClientConfigAction);
                hashCode.Add(Extension.DefaultPageSize);

                _serviceProviderHash = hashCode.ToHashCode();
            }

            return _serviceProviderHash.Value;
        }

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is DynamoOptionsExtensionInfo otherInfo
                && ReferenceEquals(Extension.DynamoDbClient, otherInfo.Extension.DynamoDbClient)
                && ReferenceEquals(
                    Extension.DynamoDbClientConfig,
                    otherInfo.Extension.DynamoDbClientConfig)
                && ReferenceEquals(
                    Extension.DynamoDbClientConfigAction,
                    otherInfo.Extension.DynamoDbClientConfigAction)
                && Extension.DefaultPageSize == otherInfo.Extension.DefaultPageSize;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) { }

        public override bool IsDatabaseProvider => true;

        public override string LogFragment
        {
            get
            {
                field ??= $"DefaultPageSize={Extension.DefaultPageSize?.ToString() ?? "null"},"
                    + $"DynamoDbClient={Extension.DynamoDbClient is not null},"
                    + $"DynamoDbClientConfig={Extension.DynamoDbClientConfig is not null},"
                    + $"DynamoDbClientConfigAction={Extension.DynamoDbClientConfigAction is not null}";
                return field;
            }
        }
    }
}
