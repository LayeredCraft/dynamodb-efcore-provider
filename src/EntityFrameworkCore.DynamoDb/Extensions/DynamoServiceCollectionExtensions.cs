using EntityFrameworkCore.DynamoDb.Diagnostics.Internal;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using EntityFrameworkCore.DynamoDb.Metadata.Conventions;
using EntityFrameworkCore.DynamoDb.Query;
using EntityFrameworkCore.DynamoDb.Query.Internal;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.DynamoDb.Extensions;

/// <summary>Represents the DynamoServiceCollectionExtensions type.</summary>
public static class DynamoServiceCollectionExtensions
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>Registers Entity Framework Core DynamoDB provider services.</summary>
        public IServiceCollection AddEntityFrameworkDynamo()
        {
            var builder = new EntityFrameworkServicesBuilder(serviceCollection)
                .TryAdd<LoggingDefinitions, DynamoLoggingDefinition>()
                .TryAdd<IDatabaseProvider, DatabaseProvider<DynamoDbOptionsExtension>>()
                .TryAdd<IDatabase, DynamoDatabaseWrapper>()
                .TryAdd<IQueryContextFactory, DynamoQueryContextFactory>()
                .TryAdd<IProviderConventionSetBuilder, DynamoConventionSetBuilder>()
                .TryAdd<IModelValidator, DynamoModelValidator>()
                .TryAdd<IModelRuntimeInitializer, DynamoModelRuntimeInitializer>()
                .TryAdd<ITypeMappingSource, DynamoTypeMappingSource>()
                .TryAdd<IQueryableMethodTranslatingExpressionVisitorFactory,
                    DynamoQueryableMethodTranslatingExpressionVisitorFactory>()
                .TryAdd<IQueryTranslationPostprocessorFactory,
                    DynamoQueryTranslationPostprocessorFactory>()
                .TryAdd<IShapedQueryCompilingExpressionVisitorFactory,
                    DynamoShapedQueryCompilingExpressionVisitorFactory>()
                .TryAdd<IQueryCompilationContextFactory, DynamoQueryCompilationContextFactory>()
                .TryAddProviderSpecificServices(services => services
                    .TryAddScoped<IDynamoClientWrapper, DynamoClientWrapper>()
                    .TryAddScoped<DynamoEntityItemSerializerSource>(_
                        => new DynamoEntityItemSerializerSource())
                    .TryAddSingleton<ISqlExpressionFactory, SqlExpressionFactory>()
                    .TryAddSingleton<IDynamoQuerySqlGeneratorFactory,
                        DynamoQuerySqlGeneratorFactory>()
                    // Replaceable via ReplaceService<IDynamoIndexSelectionAnalyzer, T>() for
                    // custom selection logic or test substitution.
                    .TryAddSingleton<IDynamoIndexSelectionAnalyzer,
                        DynamoAutoIndexSelectionAnalyzer>());

            builder.TryAddCoreServices();

            return serviceCollection;
        }
    }
}
