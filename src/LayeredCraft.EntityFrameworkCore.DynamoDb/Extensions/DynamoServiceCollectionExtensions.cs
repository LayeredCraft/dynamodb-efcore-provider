using LayeredCraft.EntityFrameworkCore.DynamoDb.Diagnostics.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
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
                .TryAdd<LoggingDefinitions, DynamoLoggingDefinition>()
                .TryAdd<IDatabaseProvider, DatabaseProvider<DynamoOptionsExtension>>()
                .TryAdd<IDatabase, DynamoDatabaseWrapper>()
                .TryAdd<IQueryContextFactory, DynamoQueryContextFactory>()
                .TryAdd<ITypeMappingSource, DynamoTypeMappingSource>()
                .TryAdd<
                    IQueryableMethodTranslatingExpressionVisitorFactory,
                    DynamoQueryableMethodTranslatingExpressionVisitorFactory
                >()
                .TryAdd<
                    IShapedQueryCompilingExpressionVisitorFactory,
                    DynamoShapedQueryCompilingExpressionVisitorFactory
                >()
                .TryAdd<IQueryCompilationContextFactory, DynamoQueryCompilationContextFactory>();

            builder.TryAddCoreServices();

            return serviceCollection;
        }
    }
}
