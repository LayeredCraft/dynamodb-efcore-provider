using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Metadata;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventionTable;

/// <summary>
///     DbContext for naming convention integration tests. Configures
///     <see cref="DynamoAttributeNamingConvention.SnakeCase" /> on the entity type so that all CLR
///     property names are transformed to snake_case DynamoDB attribute names, except
///     <c>ExplicitOverride</c> which has an explicit <c>HasAttributeName</c> override.
/// </summary>
public class NamingConventionTableDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Provides functionality for this member.</summary>
    public DbSet<NamingConventionItem> Items { get; set; } = null!;

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static NamingConventionTableDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<NamingConventionTableDbContext>()
                .UseDynamo(options => options.DynamoDbClient(client))
                .Options);

    /// <summary>Provides functionality for this member.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<NamingConventionItem>(b =>
        {
            b.ToTable(NamingConventionItemTable.TableName);
            b.HasPartitionKey(x => x.Pk);
            b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.SnakeCase);
            // Explicit override: this property maps to "custom_attr", not "explicit_override"
            b.Property(x => x.ExplicitOverride).HasAttributeName("custom_attr");
        });
}
