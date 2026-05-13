using EntityFrameworkCore.DynamoDb.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata;

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
        var indexBuilder = modelBuilder.Entity<Order>().HasIndex(x => x.CustomerId, "ByCustomer");

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
        optionsBuilder
            .UseDynamo()
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

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

    /// <summary>
    ///     Verifies that <c>HasSecondaryIndexName</c> on <see cref="IConventionIndexBuilder" />
    ///     returns <see langword="null" /> when a higher-precedence (data annotation) value is already
    ///     set.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void
        ConventionIndexBuilder_HasSecondaryIndexName_ReturnsNullWhenPrecededByDataAnnotation()
    {
        var modelBuilder = new ModelBuilder();
        var indexBuilder = modelBuilder
            .Entity<Order>()
            .HasIndex([nameof(Order.CustomerId)], "ByCustomer");
        var conventionBuilder = indexBuilder.GetInfrastructure();

        conventionBuilder.HasSecondaryIndexName("AnnotationName", true);

        var result = conventionBuilder.HasSecondaryIndexName("FluentName", false);

        result.Should().BeNull();
        indexBuilder.Metadata.GetSecondaryIndexName().Should().Be("AnnotationName");
    }

    /// <summary>
    ///     Verifies that <c>HasSecondaryIndexKind</c> on <see cref="IConventionIndexBuilder" />
    ///     returns <see langword="null" /> when a higher-precedence (data annotation) value is already
    ///     set.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void
        ConventionIndexBuilder_HasSecondaryIndexKind_ReturnsNullWhenPrecededByDataAnnotation()
    {
        var modelBuilder = new ModelBuilder();
        var indexBuilder = modelBuilder
            .Entity<Order>()
            .HasIndex([nameof(Order.CustomerId)], "ByCustomer");
        var conventionBuilder = indexBuilder.GetInfrastructure();

        conventionBuilder.HasSecondaryIndexKind(DynamoSecondaryIndexKind.Global, true);

        var result = conventionBuilder.HasSecondaryIndexKind(DynamoSecondaryIndexKind.Local, false);

        result.Should().BeNull();
        indexBuilder.Metadata.GetSecondaryIndexKind().Should().Be(DynamoSecondaryIndexKind.Global);
    }

    /// <summary>
    ///     Verifies that <c>HasSecondaryIndexProjectionType</c> on
    ///     <see cref="IConventionIndexBuilder" /> returns <see langword="null" /> when a higher-precedence
    ///     (data annotation) value is already set.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void
        ConventionIndexBuilder_HasSecondaryIndexProjectionType_ReturnsNullWhenPrecededByDataAnnotation()
    {
        var modelBuilder = new ModelBuilder();
        var indexBuilder = modelBuilder
            .Entity<Order>()
            .HasIndex([nameof(Order.CustomerId)], "ByCustomer");
        var conventionBuilder = indexBuilder.GetInfrastructure();

        conventionBuilder.HasSecondaryIndexProjectionType(
            DynamoSecondaryIndexProjectionType.KeysOnly,
            true);

        var result = conventionBuilder.HasSecondaryIndexProjectionType(
            DynamoSecondaryIndexProjectionType.All,
            false);

        result.Should().BeNull();
        indexBuilder
            .Metadata
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
                    .HasSecondaryIndexProjectionType(DynamoSecondaryIndexProjectionType.KeysOnly);
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
