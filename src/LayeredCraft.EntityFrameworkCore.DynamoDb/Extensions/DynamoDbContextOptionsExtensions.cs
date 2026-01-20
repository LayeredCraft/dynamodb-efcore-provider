using Amazon.DynamoDBv2;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Microsoft.EntityFrameworkCore;

public static class DynamoDbContextOptionsExtensions
{
    extension(DbContextOptionsBuilder optionsBuilder)
    {
        public DbContextOptionsBuilder UseDynamo()
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);

            var extension =
                optionsBuilder.Options.FindExtension<DynamoOptionsExtension>()
                ?? new DynamoOptionsExtension();

            ConfigureWarnings(optionsBuilder);

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(
                extension
            );

            return optionsBuilder;
        }

        public DbContextOptionsBuilder UseDynamo(IAmazonDynamoDB client)
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);

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
            coreOptionsExtension
        );
    }
}
