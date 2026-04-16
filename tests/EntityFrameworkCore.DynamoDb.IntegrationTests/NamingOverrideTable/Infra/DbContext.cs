using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingOverrideTable.Infra;

/// <summary>Represents the OwnedTypesTableDbContext type.</summary>
public class NamingConventionsTableDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Provides functionality for this member.</summary>
    public DbSet<QuestionItem> Items => Set<QuestionItem>();

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static NamingConventionsTableDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<NamingConventionsTableDbContext>()
                .UseDynamo(options => options.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

    /// <summary>Configures the owned-shape model used by materialization tests.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<QuestionItem>(builder =>
        {
            builder.ToTable(NamingConventionsItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            builder.Property(x => x.Pk).HasAttributeName("pk");
            builder.Property(x => x.Sk).HasAttributeName("sk");
            builder.HasGlobalSecondaryIndex("gs1-index", x => x.Gs1Pk, x => x.Gs1Sk);
            builder.HasGlobalSecondaryIndex("gs2-index", x => x.Gs2Pk, x => x.Gs2Sk);
            builder.Property(x => x.Gs1Pk).HasAttributeName("gs1-pk");
            builder.Property(x => x.Gs1Sk).HasAttributeName("gs1-sk");
            builder.Property(x => x.Gs2Pk).HasAttributeName("gs2-pk");
            builder.Property(x => x.Gs2Sk).HasAttributeName("gs2-sk");
        });
}
