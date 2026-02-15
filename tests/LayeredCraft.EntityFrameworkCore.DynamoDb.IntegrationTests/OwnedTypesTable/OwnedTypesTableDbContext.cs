using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

public class OwnedTypesTableDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<OwnedShapeItem> Items { get; set; }

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static OwnedTypesTableDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<OwnedTypesTableDbContext>().UseDynamo(options
                    => options.DynamoDbClient(client))
                .Options);

    /// <summary>Configures the owned-shape model used by materialization tests.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<OwnedShapeItem>(builder =>
        {
            builder.ToTable(OwnedTypesTableDynamoFixture.TableName);
            builder.HasKey(x => x.Pk);

            // builder.OwnsOne(
            //     x => x.Profile,
            //     profileBuilder =>
            //     {
            //         profileBuilder.OwnsOne(
            //             x => x.Address,
            //             addressBuilder =>
            //             {
            //                 addressBuilder.OwnsOne(x => x.Geo);
            //             });
            //     });
            //
            // builder.OwnsMany(
            //     x => x.Orders,
            //     orderBuilder =>
            //     {
            //         orderBuilder.OwnsOne(
            //             x => x.Payment,
            //             paymentBuilder =>
            //             {
            //                 paymentBuilder.OwnsOne(x => x.Card);
            //             });
            //
            //         orderBuilder.OwnsMany(x => x.Lines);
            //     });
            //
            // builder.OwnsMany(x => x.OrderSnapshots);
        });
}
