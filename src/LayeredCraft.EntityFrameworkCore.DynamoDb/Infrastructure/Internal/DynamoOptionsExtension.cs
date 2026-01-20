using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;

public class DynamoOptionsExtension : IDbContextOptionsExtension
{
    public void ApplyServices(IServiceCollection services) => throw new NotImplementedException();

    public void Validate(IDbContextOptions options) => throw new NotImplementedException();

    public DbContextOptionsExtensionInfo Info { get; }
}
