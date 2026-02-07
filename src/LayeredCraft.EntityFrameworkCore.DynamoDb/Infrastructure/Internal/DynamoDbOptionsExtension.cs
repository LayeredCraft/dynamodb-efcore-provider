using LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

public class DynamoDbOptionsExtension : IDbContextOptionsExtension
{
    public string? AuthenticationRegion { get; private set; }
    public string? ServiceUrl { get; private set; }

    /// <summary>
    ///     Controls how pagination behaves for queries with result limits. Default is Auto (smart
    ///     defaults based on query type).
    /// </summary>
    public DynamoPaginationMode PaginationMode { get; private set; } = DynamoPaginationMode.Auto;

    /// <summary>
    ///     The default number of items DynamoDB should evaluate per request. Null means no limit
    ///     (DynamoDB scans up to 1MB per request).
    /// </summary>
    public int? DefaultPageSize { get; private set; }

    public virtual void ApplyServices(IServiceCollection services)
        => services.AddEntityFrameworkDynamo();

    public void Validate(IDbContextOptions options) { }

    public DbContextOptionsExtensionInfo Info
    {
        get
        {
            field ??= new DynamoOptionsExtensionInfo(this);
            return field;
        }
    }

    public virtual DynamoDbOptionsExtension WithAuthenticationRegion(string? region)
    {
        var clone = Clone();

        clone.AuthenticationRegion = region;

        return clone;
    }

    public virtual DynamoDbOptionsExtension WithServiceUrl(string? serviceUrl)
    {
        var clone = Clone();

        clone.ServiceUrl = serviceUrl;

        return clone;
    }

    public virtual DynamoDbOptionsExtension WithPaginationMode(DynamoPaginationMode mode)
    {
        var clone = Clone();

        clone.PaginationMode = mode;

        return clone;
    }

    public virtual DynamoDbOptionsExtension WithDefaultPageSize(int? pageSize)
    {
        if (pageSize is <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be positive.");

        var clone = Clone();

        clone.DefaultPageSize = pageSize;

        return clone;
    }

    protected virtual DynamoDbOptionsExtension Clone()
        => new()
        {
            AuthenticationRegion = AuthenticationRegion,
            ServiceUrl = ServiceUrl,
            PaginationMode = PaginationMode,
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

                hashCode.Add(Extension.AuthenticationRegion);
                hashCode.Add(Extension.ServiceUrl);
                hashCode.Add(Extension.PaginationMode);
                hashCode.Add(Extension.DefaultPageSize);

                _serviceProviderHash = hashCode.ToHashCode();
            }

            return _serviceProviderHash.Value;
        }

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is DynamoOptionsExtensionInfo otherInfo
                && Extension.AuthenticationRegion == otherInfo.Extension.AuthenticationRegion
                && Extension.ServiceUrl == otherInfo.Extension.ServiceUrl
                && Extension.PaginationMode == otherInfo.Extension.PaginationMode
                && Extension.DefaultPageSize == otherInfo.Extension.DefaultPageSize;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) { }

        public override bool IsDatabaseProvider => true;

        public override string LogFragment
        {
            get
            {
                field ??= "WILL ADD LATER";
                return field;
            }
        }
    }
}
