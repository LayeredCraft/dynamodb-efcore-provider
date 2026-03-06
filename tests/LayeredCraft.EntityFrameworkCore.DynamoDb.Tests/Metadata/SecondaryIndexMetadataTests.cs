using Microsoft.EntityFrameworkCore;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Metadata;

public class SecondaryIndexMetadataTests
{
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(x => new { x.TenantId, x.OrderId });
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);

                entity.HasGlobalSecondaryIndex("ByCustomer", x => x.CustomerId).ProjectsAll();
                entity.HasGlobalSecondaryIndex(
                    "ByCustomerCreatedAt",
                    x => x.CustomerId,
                    x => x.CreatedAtUtc);
                entity.HasLocalSecondaryIndex("ByStatus", x => x.Status);
            });
    }

    private sealed class Order
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string CustomerId { get; set; } = null!;
        public DateTime CreatedAtUtc { get; set; }
        public string Status { get; set; } = null!;
    }

    private static TestDbContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseDynamo();
        return new TestDbContext(optionsBuilder.Options);
    }

    [Fact]
    public void HasGlobalSecondaryIndex_PartitionKeyOnly_ConfiguresSecondaryIndexMetadata()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(Order))!;
        var index = entityType.GetIndexes().Single(x => x.Name == "ByCustomer");

        index.Properties.Select(x => x.Name).Should().Equal("CustomerId");
        index.GetSecondaryIndexName().Should().Be("ByCustomer");
        index.GetSecondaryIndexKind().Should().Be(DynamoSecondaryIndexKind.Global);
        index.GetSecondaryIndexProjectionType().Should().Be(DynamoSecondaryIndexProjectionType.All);
    }

    [Fact]
    public void HasGlobalSecondaryIndex_PartitionAndSortKey_ConfiguresSecondaryIndexMetadata()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(Order))!;
        var index = entityType.GetIndexes().Single(x => x.Name == "ByCustomerCreatedAt");

        index.Properties.Select(x => x.Name).Should().Equal("CustomerId", "CreatedAtUtc");
        index.GetSecondaryIndexName().Should().Be("ByCustomerCreatedAt");
        index.GetSecondaryIndexKind().Should().Be(DynamoSecondaryIndexKind.Global);
        index.GetSecondaryIndexProjectionType().Should().Be(DynamoSecondaryIndexProjectionType.All);
    }

    [Fact]
    public void HasLocalSecondaryIndex_UsesConfiguredPartitionKeyAndSortKey()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(Order))!;
        var index = entityType.GetIndexes().Single(x => x.Name == "ByStatus");

        index.Properties.Select(x => x.Name).Should().Equal("TenantId", "Status");
        index.GetSecondaryIndexName().Should().Be("ByStatus");
        index.GetSecondaryIndexKind().Should().Be(DynamoSecondaryIndexKind.Local);
        index.GetSecondaryIndexProjectionType().Should().Be(DynamoSecondaryIndexProjectionType.All);
    }

    [Fact]
    public void HasLocalSecondaryIndex_WithoutConfiguredPartitionKey_ThrowsHelpfulError()
    {
        var optionsBuilder = new DbContextOptionsBuilder<MissingPartitionKeyContext>();
        optionsBuilder.UseDynamo();

        Action act = () =>
        {
            using var context = new MissingPartitionKeyContext(optionsBuilder.Options);
            _ = context.Model;
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must configure a partition key before configuring a local secondary index*");
    }

    private sealed class MissingPartitionKeyContext(DbContextOptions<MissingPartitionKeyContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MissingPartitionKeyOrder>(entity =>
            {
                entity.HasKey(x => new { x.TenantId, x.OrderId });
                entity.HasLocalSecondaryIndex("ByStatus", x => x.Status);
            });
    }

    private sealed class MissingPartitionKeyOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string Status { get; set; } = null!;
    }
}
