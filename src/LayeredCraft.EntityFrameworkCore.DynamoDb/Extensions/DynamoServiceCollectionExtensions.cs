using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;

public static class DynamoServiceCollectionExtensions
{
    extension(IServiceCollection serviceCollection)
    {
        public IServiceCollection AddEntityFrameworkDynamo()
        {
            var builder = new EntityFrameworkServicesBuilder(serviceCollection)
                .TryAdd<IDatabaseProvider, DatabaseProvider<DynamoOptionsExtension>>()
                .TryAdd<IDatabase, DynamoDatabaseWrapper>();

            builder.TryAddCoreServices();

            return serviceCollection;
        }
    }
}
