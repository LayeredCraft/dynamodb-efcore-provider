using LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

public class DynamoOptionsExtension : IDbContextOptionsExtension
{
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

    protected virtual DynamoOptionsExtension Clone() => new();

    public class DynamoOptionsExtensionInfo : DbContextOptionsExtensionInfo
    {
        public DynamoOptionsExtensionInfo(IDbContextOptionsExtension extension)
            : base(extension) { }

        public override int GetServiceProviderHashCode() => 1;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) =>
            throw new NotImplementedException();

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) { }

        public override bool IsDatabaseProvider { get; }
        public override string LogFragment { get; }
    }
}
