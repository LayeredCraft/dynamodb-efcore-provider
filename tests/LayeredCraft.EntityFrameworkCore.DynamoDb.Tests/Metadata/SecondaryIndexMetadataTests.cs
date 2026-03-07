using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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
            .WithMessage("*no DynamoDB sort key is configured*before configuring an LSI*");
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
    public void DerivedTypeHasLocalSecondaryIndex_UsingTableSortKey_ThrowsHelpfulError()
    {
        var optionsBuilder = new DbContextOptionsBuilder<DerivedDuplicateSortKeyContext>();
        optionsBuilder.UseDynamo();

        Action act = () =>
        {
            using var context = new DerivedDuplicateSortKeyContext(optionsBuilder.Options);
            _ = context.Model;
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must use an alternate sort key different from the table sort key*");
    }

    [Fact]
    public void HasGlobalSecondaryIndex_PartitionKeyUnsupportedType_ThrowsHelpfulError()
    {
        var optionsBuilder = new DbContextOptionsBuilder<UnsupportedGlobalPartitionKeyTypeContext>();
        optionsBuilder.UseDynamo();

        Action act = () =>
        {
            using var context = new UnsupportedGlobalPartitionKeyTypeContext(optionsBuilder.Options);
            _ = context.Model;
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*secondary index 'ByPriority'*global secondary index partition key*must be string, number, or binary*");
    }

    [Fact]
    public void HasGlobalSecondaryIndex_SortKeyUnsupportedType_ThrowsHelpfulError()
    {
        var optionsBuilder = new DbContextOptionsBuilder<UnsupportedGlobalSortKeyTypeContext>();
        optionsBuilder.UseDynamo();

        Action act = () =>
        {
            using var context = new UnsupportedGlobalSortKeyTypeContext(optionsBuilder.Options);
            _ = context.Model;
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*secondary index 'ByCustomerPriority'*global secondary index sort key*must be string, number, or binary*");
    }

    [Fact]
    public void HasLocalSecondaryIndex_SortKeyUnsupportedType_ThrowsHelpfulError()
    {
        var optionsBuilder = new DbContextOptionsBuilder<UnsupportedLocalSortKeyTypeContext>();
        optionsBuilder.UseDynamo();

        Action act = () =>
        {
            using var context = new UnsupportedLocalSortKeyTypeContext(optionsBuilder.Options);
            _ = context.Model;
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*secondary index 'ByPriority'*local secondary index sort key*must be string, number, or binary*");
    }

    [Fact]
    public void HasGlobalSecondaryIndex_NullablePartitionKey_IsAllowedForSparseMembership()
    {
        var optionsBuilder = new DbContextOptionsBuilder<NullableGlobalPartitionKeyContext>();
        optionsBuilder.UseDynamo();

        using var context = new NullableGlobalPartitionKeyContext(optionsBuilder.Options);

        var entityType = context.Model.FindEntityType(typeof(NullableGlobalPartitionKeyOrder))!;
        var index = entityType.GetIndexes().Single(x => x.Name == "ByCustomer");

        index.GetSecondaryIndexKind().Should().Be(DynamoSecondaryIndexKind.Global);
        index.Properties.Single().Name.Should().Be(nameof(NullableGlobalPartitionKeyOrder.CustomerId));
    }

    [Fact]
    public void HasGlobalSecondaryIndex_ConverterToSupportedProviderType_DoesNotThrow()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ConverterGlobalPartitionKeyContext>();
        optionsBuilder.UseDynamo();

        Action act = () =>
        {
            using var context = new ConverterGlobalPartitionKeyContext(optionsBuilder.Options);
            _ = context.Model;
        };

        act.Should().NotThrow();
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
    public void RuntimeTableModel_IncludesDerivedTypeSecondaryIndexes()
    {
        var optionsBuilder = new DbContextOptionsBuilder<DerivedIndexContext>();
        optionsBuilder.UseDynamo();

        using var context = new DerivedIndexContext(optionsBuilder.Options);

        var runtimeTableModel = context.Model.GetDynamoRuntimeTableModel()!;
        var tableDescriptor = runtimeTableModel.Tables["Orders"];
        var sources = tableDescriptor.SourcesByEntityTypeName[typeof(BaseOrder).FullName!];

        sources.Select(x => x.IndexName).Should().Equal(null, "ByPriority", "ByStatus");
        sources[1].ModelIndex.Should().NotBeNull();
        sources[1].ModelIndex!.DeclaringEntityType.ClrType.Should().Be(typeof(PriorityOrder));
        sources[1].PartitionKeyProperty.Name.Should().Be(nameof(PriorityOrder.Priority));
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
    public void RuntimeTableModel_AllowsSharedTableTypeSpecificSecondaryIndexes()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SharedTableTypeSpecificIndexContext>();
        optionsBuilder.UseDynamo();

        using var context = new SharedTableTypeSpecificIndexContext(optionsBuilder.Options);

        var runtimeTableModel = context.Model.GetDynamoRuntimeTableModel()!;
        var tableDescriptor = runtimeTableModel.Tables["Orders"];

        tableDescriptor.SourcesByEntityTypeName[typeof(CustomerOrder).FullName!]
            .Select(source => source.IndexName)
            .Should()
            .Equal(null, "ByCustomer", "ByStatus");

        tableDescriptor.SourcesByEntityTypeName[typeof(AuditOrder).FullName!]
            .Select(source => source.IndexName)
            .Should()
            .Equal(null, "ByStatus");
    }

    [Fact]
    public void RuntimeTableModel_SharedTableSameNameDifferentKinds_ThrowsHelpfulError()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SharedTableSameNameDifferentKindsContext>();
        optionsBuilder.UseDynamo();

        Action act = () =>
        {
            using var context = new SharedTableSameNameDifferentKindsContext(optionsBuilder.Options);
            _ = context.Model;
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*map to DynamoDB table 'Orders' but expose inconsistent secondary-index metadata*");
    }

    [Fact]
    public void RuntimeTableModel_HierarchyDuplicateIndexNameWithConflictingDefinitions_IsRejected()
    {
        var optionsBuilder = new DbContextOptionsBuilder<HierarchyDuplicateIndexConflictContext>();
        optionsBuilder.UseDynamo();

        Action act = () =>
        {
            using var context = new HierarchyDuplicateIndexConflictContext(optionsBuilder.Options);
            _ = context.Model;
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*same name already exists*");
    }

    [Fact]
    public void RuntimeTableModel_HierarchyDuplicateIndexNameWithSameDefinition_IsDeduplicated()
    {
        var optionsBuilder = new DbContextOptionsBuilder<HierarchyDuplicateIndexSameDefinitionContext>();
        optionsBuilder.UseDynamo();

        using var context = new HierarchyDuplicateIndexSameDefinitionContext(optionsBuilder.Options);

        var runtimeTableModel = context.Model.GetDynamoRuntimeTableModel()!;
        var tableDescriptor = runtimeTableModel.Tables["Orders"];
        var sources = tableDescriptor.SourcesByEntityTypeName[typeof(BaseLookupOrder).FullName!];

        sources.Select(x => x.IndexName).Should().Equal(null, "ByLookup");
    }

    [Fact]
    public void RuntimeTableModel_SharedTableSecondaryIndexTypeMismatch_ThrowsHelpfulError()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SharedTableMismatchedIndexTypeContext>();
        optionsBuilder.UseDynamo();

        Action act = () =>
        {
            using var context = new SharedTableMismatchedIndexTypeContext(optionsBuilder.Options);
            _ = context.Model;
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*map to DynamoDB table 'Orders' but expose inconsistent secondary-index metadata*");
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

    private sealed class DerivedDuplicateSortKeyContext(DbContextOptions<DerivedDuplicateSortKeyContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BaseDerivedDuplicateSortKeyOrder>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
            });

            modelBuilder.Entity<DerivedDuplicateSortKeyOrder>(entity =>
            {
                entity.HasBaseType<BaseDerivedDuplicateSortKeyOrder>();
                entity.HasLocalSecondaryIndex("ByOrderId", x => x.OrderId);
            });
        }
    }

    private class BaseDerivedDuplicateSortKeyOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
    }

    private sealed class DerivedDuplicateSortKeyOrder : BaseDerivedDuplicateSortKeyOrder;

    private sealed class UnsupportedGlobalPartitionKeyTypeContext(DbContextOptions<UnsupportedGlobalPartitionKeyTypeContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UnsupportedGlobalPartitionKeyTypeOrder>(entity =>
            {
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
                entity.HasGlobalSecondaryIndex("ByPriority", x => x.IsPriority);
            });
    }

    private sealed class UnsupportedGlobalPartitionKeyTypeOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public bool IsPriority { get; set; }
    }

    private sealed class UnsupportedGlobalSortKeyTypeContext(DbContextOptions<UnsupportedGlobalSortKeyTypeContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UnsupportedGlobalSortKeyTypeOrder>(entity =>
            {
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
                entity.HasGlobalSecondaryIndex("ByCustomerPriority", x => x.CustomerId, x => x.IsPriority);
            });
    }

    private sealed class UnsupportedGlobalSortKeyTypeOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string CustomerId { get; set; } = null!;
        public bool IsPriority { get; set; }
    }

    private sealed class UnsupportedLocalSortKeyTypeContext(DbContextOptions<UnsupportedLocalSortKeyTypeContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UnsupportedLocalSortKeyTypeOrder>(entity =>
            {
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
                entity.HasLocalSecondaryIndex("ByPriority", x => x.IsPriority);
            });
    }

    private sealed class UnsupportedLocalSortKeyTypeOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public bool IsPriority { get; set; }
    }

    private sealed class NullableGlobalPartitionKeyContext(DbContextOptions<NullableGlobalPartitionKeyContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NullableGlobalPartitionKeyOrder>(entity =>
            {
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
                entity.HasGlobalSecondaryIndex("ByCustomer", x => x.CustomerId);
            });
    }

    private sealed class NullableGlobalPartitionKeyOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string? CustomerId { get; set; }
    }

    private sealed class ConverterGlobalPartitionKeyContext(DbContextOptions<ConverterGlobalPartitionKeyContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConverterGlobalPartitionKeyOrder>(entity =>
            {
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
                entity.Property(x => x.CustomerId)
                    .HasConversion(new ValueConverter<Guid, string>(
                        value => value.ToString("N"),
                        value => Guid.ParseExact(value, "N")));
                entity.HasGlobalSecondaryIndex("ByCustomer", x => x.CustomerId);
            });
    }

    private sealed class ConverterGlobalPartitionKeyOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public Guid CustomerId { get; set; }
    }

    private sealed class DerivedIndexContext(DbContextOptions<DerivedIndexContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BaseOrder>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
                entity.HasLocalSecondaryIndex("ByStatus", x => x.Status);
            });

            modelBuilder.Entity<PriorityOrder>(entity =>
            {
                entity.HasBaseType<BaseOrder>();
                entity.HasGlobalSecondaryIndex("ByPriority", x => x.Priority);
            });
        }
    }

    private class BaseOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string Status { get; set; } = null!;
    }

    private sealed class PriorityOrder : BaseOrder
    {
        public int Priority { get; set; }
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

    private sealed class SharedTableTypeSpecificIndexContext(DbContextOptions<SharedTableTypeSpecificIndexContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomerOrder>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
                entity.HasGlobalSecondaryIndex("ByCustomer", x => x.CustomerId);
                entity.HasLocalSecondaryIndex("ByStatus", x => x.Status);
            });

            modelBuilder.Entity<AuditOrder>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
                entity.HasLocalSecondaryIndex("ByStatus", x => x.Status);
            });
        }
    }

    private sealed class CustomerOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string CustomerId { get; set; } = null!;
        public string Status { get; set; } = null!;
    }

    private sealed class AuditOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string Status { get; set; } = null!;
    }

    private sealed class SharedTableSameNameDifferentKindsContext(DbContextOptions<SharedTableSameNameDifferentKindsContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SharedStatusGsiOrder>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
                entity.HasGlobalSecondaryIndex("ByStatus", x => x.Status);
            });

            modelBuilder.Entity<SharedStatusLsiOrder>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
                entity.HasLocalSecondaryIndex("ByStatus", x => x.Status);
            });
        }
    }

    private sealed class SharedStatusGsiOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string Status { get; set; } = null!;
    }

    private sealed class SharedStatusLsiOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string Status { get; set; } = null!;
    }

    private sealed class HierarchyDuplicateIndexConflictContext(DbContextOptions<HierarchyDuplicateIndexConflictContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BaseLookupOrder>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
                entity.HasGlobalSecondaryIndex("ByLookup", x => x.CustomerId);
            });

            modelBuilder.Entity<PriorityLookupOrder>(entity =>
            {
                entity.HasBaseType<BaseLookupOrder>();
                entity.HasGlobalSecondaryIndex("ByLookup", x => x.Priority);
            });
        }
    }

    private sealed class HierarchyDuplicateIndexSameDefinitionContext(DbContextOptions<HierarchyDuplicateIndexSameDefinitionContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BaseLookupOrder>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasSortKey(x => x.OrderId);
                entity.HasGlobalSecondaryIndex("ByLookup", x => x.CustomerId);
            });

            modelBuilder.Entity<DerivedLookupOrder>(entity =>
            {
                entity.HasBaseType<BaseLookupOrder>();
                entity.HasGlobalSecondaryIndex("ByLookup", x => x.CustomerId);
            });
        }
    }

    private class BaseLookupOrder
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string CustomerId { get; set; } = null!;
    }

    private sealed class PriorityLookupOrder : BaseLookupOrder
    {
        public int Priority { get; set; }
    }

    private sealed class DerivedLookupOrder : BaseLookupOrder;

    private sealed class SharedTableMismatchedIndexTypeContext(DbContextOptions<SharedTableMismatchedIndexTypeContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StringCustomerOrder>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasGlobalSecondaryIndex("ByCustomer", x => x.CustomerId);
            });

            modelBuilder.Entity<NumericCustomerOrder>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasPartitionKey(x => x.TenantId);
                entity.HasGlobalSecondaryIndex("ByCustomer", x => x.CustomerId);
            });
        }
    }

    private sealed class StringCustomerOrder
    {
        public string TenantId { get; set; } = null!;
        public string CustomerId { get; set; } = null!;
    }

    private sealed class NumericCustomerOrder
    {
        public string TenantId { get; set; } = null!;
        public int CustomerId { get; set; }
    }
}
