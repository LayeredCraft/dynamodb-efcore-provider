using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;

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

        index.Properties.Select(x => x.Name).Should().Equal("Status");
        index.GetSecondaryIndexName().Should().Be("ByStatus");
        index.GetSecondaryIndexKind().Should().Be(DynamoSecondaryIndexKind.Local);
        index.GetSecondaryIndexProjectionType().Should().Be(DynamoSecondaryIndexProjectionType.All);
    }

    [Fact]
    public void HasLocalSecondaryIndex_UsesDynamoKeyConventionWhenPartitionKeyIsNotExplicitlyConfigured()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ConventionPartitionKeyContext>();
        optionsBuilder.UseDynamo();

        using var context = new ConventionPartitionKeyContext(optionsBuilder.Options);

        var entityType = context.Model.FindEntityType(typeof(ConventionPartitionKeyOrder))!;
        var index = entityType.GetIndexes().Single(x => x.Name == "ByStatus");

        index.Properties.Select(x => x.Name).Should().Equal("Status");
    }

    [Fact]
    public void HasLocalSecondaryIndex_BeforeTableKeyConfiguration_IsAllowedUntilModelValidation()
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrderIndependentLsiContext>();
        optionsBuilder.UseDynamo();

        using var context = new OrderIndependentLsiContext(optionsBuilder.Options);

        var entityType = context.Model.FindEntityType(typeof(OrderIndependentLsiOrder))!;
        var index = entityType.GetIndexes().Single(x => x.Name == "ByStatus");

        index.Properties.Select(x => x.Name).Should().Equal("Status");
        entityType.GetPartitionKeyPropertyName().Should().Be("TenantId");
        entityType.GetSortKeyPropertyName().Should().Be("OrderId");
    }

    [Fact]
    public void HasLocalSecondaryIndex_WithoutCompositePrimaryKey_ThrowsHelpfulError()
    {
        var optionsBuilder = new DbContextOptionsBuilder<HashOnlyTableContext>();
        optionsBuilder.UseDynamo();

        Action act = () =>
        {
            using var context = new HashOnlyTableContext(optionsBuilder.Options);
            _ = context.Model;
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no DynamoDB sort key is configured*before configuring an LSI*" );
    }

    [Fact]
    public void HasLocalSecondaryIndex_UsingTableSortKey_ThrowsHelpfulError()
    {
        var optionsBuilder = new DbContextOptionsBuilder<DuplicateSortKeyContext>();
        optionsBuilder.UseDynamo();

        Action act = () =>
        {
            using var context = new DuplicateSortKeyContext(optionsBuilder.Options);
            _ = context.Model;
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must use an alternate sort key different from the table sort key*");
    }

    [Fact]
    public void Model_ContainsRuntimeTableModel_ForConfiguredSecondaryIndexes()
    {
        using var context = CreateContext();

        var runtimeTableModel = context.Model.GetDynamoRuntimeTableModel();

        runtimeTableModel.Should().NotBeNull();
        runtimeTableModel!.Tables.Should().ContainKey(nameof(Order));

        var tableDescriptor = runtimeTableModel.Tables[nameof(Order)];
        tableDescriptor.TableName.Should().Be(nameof(Order));
        tableDescriptor.RootEntityTypes.Should().ContainSingle();
        tableDescriptor.RootEntityTypes[0].ClrType.Should().Be(typeof(Order));
    }

    [Fact]
    public void RuntimeTableModel_BuildsOrderedSourcesWithResolvedMetadataObjects()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(Order))!;
        var runtimeTableModel = context.Model.GetDynamoRuntimeTableModel()!;
        var tableDescriptor = runtimeTableModel.Tables[nameof(Order)];
        var sources = tableDescriptor.SourcesByEntityTypeName[entityType.Name];

        sources.Select(x => x.IndexName).Should().Equal(null, "ByCustomer", "ByCustomerCreatedAt", "ByStatus");
        sources.Select(x => x.Kind).Should().Equal(
            DynamoIndexSourceKind.Table,
            DynamoIndexSourceKind.GlobalSecondaryIndex,
            DynamoIndexSourceKind.GlobalSecondaryIndex,
            DynamoIndexSourceKind.LocalSecondaryIndex);

        sources[0].PartitionKeyProperty.Name.Should().Be(nameof(Order.TenantId));
        sources[0].SortKeyProperty!.Name.Should().Be(nameof(Order.OrderId));
        sources[1].PartitionKeyProperty.Name.Should().Be(nameof(Order.CustomerId));
        sources[1].SortKeyProperty.Should().BeNull();
        sources[2].PartitionKeyProperty.Name.Should().Be(nameof(Order.CustomerId));
        sources[2].SortKeyProperty!.Name.Should().Be(nameof(Order.CreatedAtUtc));
        sources[3].PartitionKeyProperty.Name.Should().Be(nameof(Order.TenantId));
        sources[3].SortKeyProperty!.Name.Should().Be(nameof(Order.Status));

        sources.Should().OnlyContain(x => x.ProjectionType == DynamoSecondaryIndexProjectionType.All);
    }

    [Fact]
    public void RuntimeTableModel_CanonicalizesSharedTableMappings()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SharedTableContext>();
        optionsBuilder.UseDynamo();

        using var context = new SharedTableContext(optionsBuilder.Options);

        var runtimeTableModel = context.Model.GetDynamoRuntimeTableModel()!;

        runtimeTableModel.Tables.Should().ContainSingle();
        runtimeTableModel.Tables.Should().ContainKey("Orders");

        var tableDescriptor = runtimeTableModel.Tables["Orders"];
        tableDescriptor.RootEntityTypes.Select(x => x.ClrType).Should().BeEquivalentTo(new[]
        {
            typeof(LiveOrder),
            typeof(ArchivedOrder),
        });
        tableDescriptor.SourcesByEntityTypeName.Should().HaveCount(2);
        tableDescriptor.SourcesByEntityTypeName.Values.Should().OnlyContain(x => x.Count == 3);
        tableDescriptor.SourcesByEntityTypeName.Values.Should().OnlyContain(
            x => x.Select(source => source.IndexName).SequenceEqual(new string?[] { null, "ByCustomer", "ByStatus" }));
    }

    [Fact]
    public void ProviderServices_RegisterCustomModelRuntimeInitializer()
    {
        using var context = CreateContext();

        var runtimeInitializer = context.GetService<IModelRuntimeInitializer>();

        runtimeInitializer.Should().BeOfType<DynamoModelRuntimeInitializer>();
    }

    private sealed class ConventionPartitionKeyContext(DbContextOptions<ConventionPartitionKeyContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConventionPartitionKeyOrder>(entity =>
            {
                entity.HasPartitionKey(x => x.PK);
                entity.HasSortKey(x => x.SK);
                entity.HasLocalSecondaryIndex("ByStatus", x => x.Status);
            });
    }

    private sealed class OrderIndependentLsiContext(DbContextOptions<OrderIndependentLsiContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OrderIndependentLsiOrder>(entity =>
            {
                entity.HasLocalSecondaryIndex("ByStatus", x => x.Status);
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
            });
    }

    private sealed class OrderIndependentLsiOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string Status { get; set; } = null!;
    }

    private sealed class ConventionPartitionKeyOrder
    {
        public string PK { get; set; } = null!;
        public string SK { get; set; } = null!;
        public string Status { get; set; } = null!;
    }

    private sealed class HashOnlyTableContext(DbContextOptions<HashOnlyTableContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<HashOnlyTableOrder>(entity =>
            {
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasLocalSecondaryIndex("ByStatus", x => x.Status);
            });
    }

    private sealed class HashOnlyTableOrder
    {
        public string TenantId { get; set; } = null!;
        public string Status { get; set; } = null!;
    }

    private sealed class DuplicateSortKeyContext(DbContextOptions<DuplicateSortKeyContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DuplicateSortKeyOrder>(entity =>
            {
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
                entity.HasLocalSecondaryIndex("ByOrderId", x => x.OrderId);
            });
    }

    private sealed class DuplicateSortKeyOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
    }

    private sealed class SharedTableContext(DbContextOptions<SharedTableContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureSharedOrder(modelBuilder.Entity<LiveOrder>());
            ConfigureSharedOrder(modelBuilder.Entity<ArchivedOrder>());
        }

        private static void ConfigureSharedOrder<TEntity>(EntityTypeBuilder<TEntity> entity)
            where TEntity : class
        {
            entity.ToTable("Orders");
            entity.HasPartitionKey("TenantId");
            entity.HasSortKey("OrderId");
            entity.HasGlobalSecondaryIndex("ByCustomer", "CustomerId");
            entity.HasLocalSecondaryIndex("ByStatus", "Status");
        }
    }

    private sealed class LiveOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string CustomerId { get; set; } = null!;
        public string Status { get; set; } = null!;
    }

    private sealed class ArchivedOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string CustomerId { get; set; } = null!;
        public string Status { get; set; } = null!;
    }
}
