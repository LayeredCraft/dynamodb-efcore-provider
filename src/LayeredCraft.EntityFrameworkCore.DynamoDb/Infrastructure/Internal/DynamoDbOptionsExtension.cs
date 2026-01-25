using LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

public class DynamoDbOptionsExtension : IDbContextOptionsExtension
{
    public string? AuthenticationRegion { get; private set; }
    public string? ServiceUrl { get; private set; }

    public virtual void ApplyServices(IServiceCollection services) =>
        services.AddEntityFrameworkDynamo();

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

    protected virtual DynamoDbOptionsExtension Clone() => new();

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

                _serviceProviderHash = hashCode.ToHashCode();
            }

            return _serviceProviderHash.Value;
        }

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) =>
            other is DynamoOptionsExtensionInfo otherInfo
            && Extension.AuthenticationRegion == otherInfo.Extension.AuthenticationRegion
            && Extension.ServiceUrl == otherInfo.Extension.ServiceUrl;

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
