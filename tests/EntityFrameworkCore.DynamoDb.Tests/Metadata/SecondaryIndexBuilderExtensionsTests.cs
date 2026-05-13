using EntityFrameworkCore.DynamoDb.Metadata;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata;

/// <summary>Represents the SecondaryIndexBuilderExtensionsTests type.</summary>
public class SecondaryIndexBuilderExtensionsTests
{
    /// <summary>Verifies that untyped index builder APIs configure DynamoDB secondary index annotations.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void IndexBuilder_ConfiguresSecondaryIndexMetadata()
    {
        var modelBuilder = new ModelBuilder();
        var indexBuilder = modelBuilder
            .Entity<Order>()
            .HasIndex([nameof(Order.CustomerId)], "ByCustomer");

        var returned = indexBuilder
            .HasSecondaryIndexName("ByCustomerIndex")
            .HasSecondaryIndexKind(DynamoSecondaryIndexKind.Global)
            .HasSecondaryIndexProjectionType(DynamoSecondaryIndexProjectionType.KeysOnly);

        returned.Should().BeSameAs(indexBuilder);
        indexBuilder.Metadata.GetSecondaryIndexName().Should().Be("ByCustomerIndex");
        indexBuilder.Metadata.GetSecondaryIndexKind().Should().Be(DynamoSecondaryIndexKind.Global);
        indexBuilder
            .Metadata
            .GetSecondaryIndexProjectionType()
            .Should()
            .Be(DynamoSecondaryIndexProjectionType.KeysOnly);
    }

    /// <summary>Verifies that typed index builder APIs configure DynamoDB secondary index annotations.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void TypedIndexBuilder_ConfiguresSecondaryIndexMetadata()
    {
        var modelBuilder = new ModelBuilder();
        var indexBuilder = modelBuilder.Entity<Order>().HasIndex(x => x.CustomerId);

        var returned = indexBuilder
            .HasSecondaryIndexName("ByCustomer")
            .HasSecondaryIndexKind(DynamoSecondaryIndexKind.Global)
            .HasSecondaryIndexProjectionType(DynamoSecondaryIndexProjectionType.All);

        returned.Should().BeSameAs(indexBuilder);
        indexBuilder.Metadata.GetSecondaryIndexName().Should().Be("ByCustomer");
        indexBuilder.Metadata.GetSecondaryIndexKind().Should().Be(DynamoSecondaryIndexKind.Global);
        indexBuilder
            .Metadata
            .GetSecondaryIndexProjectionType()
            .Should()
            .Be(DynamoSecondaryIndexProjectionType.All);
    }

    /// <summary>Verifies that secondary index builder APIs configure projection metadata fluently.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SecondaryIndexBuilder_ConfiguresProjectionMetadata()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ProjectionContext>();
        optionsBuilder.UseDynamo();

        using var context = new ProjectionContext(optionsBuilder.Options);

        var index =
            context.Model.FindEntityType(typeof(Order))!
                .GetIndexes()
                .Single(x => x.Name == "ByCustomer");

        index
            .GetSecondaryIndexProjectionType()
            .Should()
            .Be(DynamoSecondaryIndexProjectionType.KeysOnly);
    }

    private sealed class ProjectionContext(DbContextOptions<ProjectionContext> options) : DbContext(
        options)
    {
        /// <summary>Configures the test model.</summary>
        /// <param name="modelBuilder">The model builder.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Order>(entity =>
            {
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
                entity
                    .HasGlobalSecondaryIndex("ByCustomer", x => x.CustomerId)
                    .HasProjectionType(DynamoSecondaryIndexProjectionType.KeysOnly);
            });
    }

    private sealed class Order
    {
        /// <summary>Gets or sets the tenant ID.</summary>
        public string TenantId { get; set; } = null!;

        /// <summary>Gets or sets the order ID.</summary>
        public string OrderId { get; set; } = null!;

        /// <summary>Gets or sets the customer ID.</summary>
        public string CustomerId { get; set; } = null!;
    }
}
