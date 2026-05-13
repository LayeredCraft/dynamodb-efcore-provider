using EntityFrameworkCore.DynamoDb.Diagnostics;
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

    extension<TContext>(DbContextOptionsBuilder<TContext> optionsBuilder) where TContext : DbContext
    {
        /// <summary>Configures this context to use the DynamoDB provider.</summary>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public DbContextOptionsBuilder<TContext> UseDynamo()
            => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder)
                .UseDynamo();

        /// <summary>Configures this context to use the DynamoDB provider.</summary>
        /// <param name="configure">Provider-specific options to configure.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public DbContextOptionsBuilder<TContext> UseDynamo(
            Action<DynamoDbContextOptionsBuilder> configure)
            => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder)
                .UseDynamo(configure);
    }

    private static void ConfigureWarnings(DbContextOptionsBuilder optionsBuilder)
    {
        var coreOptionsExtension =
            optionsBuilder.Options.FindExtension<CoreOptionsExtension>()
            ?? new CoreOptionsExtension();

        coreOptionsExtension = coreOptionsExtension.WithWarningsConfiguration(
            coreOptionsExtension.WarningsConfiguration.TryWithExplicit(
                DynamoEventId.ScanLikeQueryDetected,
                WarningBehavior.Throw));

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(
            coreOptionsExtension);
    }
}
