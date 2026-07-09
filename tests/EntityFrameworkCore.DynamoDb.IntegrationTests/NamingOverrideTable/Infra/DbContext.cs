using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingOverrideTable.Infra;

/// <summary>DbContext for explicit attribute-name override integration tests.</summary>
public class NamingOverridesTableDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<QuestionItem> Items => Set<QuestionItem>();

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static NamingOverridesTableDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<NamingOverridesTableDbContext>()
                .UseDynamo(options => options.DynamoDbClient(client))
                .ConfigureWarnings(w
                    => w
                        .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                        .Ignore(DynamoEventId.ScanLikeQueryDetected))
                .Options);

    /// <summary>Configures the owned-shape model used by materialization tests.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<QuestionItem>(builder =>
        {
            builder.ToTable(NamingOverridesItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            builder.HasGlobalSecondaryIndex("gs1-index", x => x.Gs1Pk, x => x.Gs1Sk);
            builder.HasGlobalSecondaryIndex("gs2-index", x => x.Gs2Pk, x => x.Gs2Sk);
            builder.Property(x => x.Gs1Pk).HasAttributeName("gs1-pk");
            builder.Property(x => x.Gs1Sk).HasAttributeName("gs1-sk");
            builder.Property(x => x.Gs2Pk).HasAttributeName("gs2-pk");
            builder.Property(x => x.Gs2Sk).HasAttributeName("gs2-sk");
        });
}
