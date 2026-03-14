using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Microsoft.EntityFrameworkCore;

/// <summary>Represents the DynamoDbContextOptionsExtensions type.</summary>
public static class DynamoDbContextOptionsExtensions
{
    extension(DbContextOptionsBuilder optionsBuilder)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbContextOptionsBuilder UseDynamo()
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);

            var extension =
                optionsBuilder.Options.FindExtension<DynamoDbOptionsExtension>()
                ?? new DynamoDbOptionsExtension();

            ConfigureWarnings(optionsBuilder);

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder)
                .AddOrUpdateExtension(extension);

            return optionsBuilder;
        }

        /// <summary>Provides functionality for this member.</summary>
        public DbContextOptionsBuilder UseDynamo(Action<DynamoDbContextOptionsBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);
            ArgumentNullException.ThrowIfNull(configure);

            var extension =
                optionsBuilder.Options.FindExtension<DynamoDbOptionsExtension>()
                ?? new DynamoDbOptionsExtension();

            ConfigureWarnings(optionsBuilder);

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder)
                .AddOrUpdateExtension(extension);

            configure.Invoke(new DynamoDbContextOptionsBuilder(optionsBuilder));

            return optionsBuilder;
        }
    }

    private static void ConfigureWarnings(DbContextOptionsBuilder optionsBuilder)
    {
        var coreOptionsExtension =
            optionsBuilder.Options.FindExtension<CoreOptionsExtension>()
            ?? new CoreOptionsExtension();

        // coreOptionsExtension = coreOptionsExtension.WithWarningsConfiguration(
        //     coreOptionsExtension.WarningsConfiguration.TryWithExplicit(
        //         CosmosEventId.BulkExecutionWithTransactionalBatch, WarningBehavior.Throw));

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(
            coreOptionsExtension);
    }
}
