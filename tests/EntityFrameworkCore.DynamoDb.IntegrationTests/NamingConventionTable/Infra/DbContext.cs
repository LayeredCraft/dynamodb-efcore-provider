using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Metadata;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventionTable;

/// <summary>
///     DbContext for naming convention integration tests. Maps two entity types to separate
///     DynamoDB tables — one with <see cref="DynamoAttributeNamingConvention.SnakeCase" /> and one
///     with <see cref="DynamoAttributeNamingConvention.CamelCase" /> — demonstrating that naming
///     conventions are applied per-entity, independently of one another.
/// </summary>
public class NamingConventionTableDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Snake_case entity set.</summary>
    public DbSet<SnakeCaseItem> SnakeCaseItems { get; set; } = null!;

    /// <summary>Kebab-case entity set.</summary>
    public DbSet<KebabCaseItem> KebabCaseItems { get; set; } = null!;

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static NamingConventionTableDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<NamingConventionTableDbContext>()
                .UseDynamo(options => options.DynamoDbClient(client))
                .Options);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SnakeCaseItem>(b =>
        {
            b.ToTable(SnakeCaseItemTable.TableName);
            b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.SnakeCase);
            // Explicit per-property override — stored as "custom_attr", not "explicit_override"
            b.Property(x => x.ExplicitOverride).HasAttributeName("custom_attr");
        });

        modelBuilder.Entity<KebabCaseItem>(b =>
        {
            b.ToTable(KebabCaseItemTable.TableName);
            b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.KebabCase);
        });
    }
}
