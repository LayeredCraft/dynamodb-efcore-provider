using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Metadata;

public class DiscriminatorMetadataTests
{
    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

    private sealed record UserEntity
    {
        public string Id { get; set; } = null!;
    }

    private sealed record OrderEntity
    {
        public string Id { get; set; } = null!;
    }

    private sealed class SingleTypeContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("Users");
                b.HasPartitionKey(x => x.Id);
            });

        public static SingleTypeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SingleTypeContext>(client));
    }

    private sealed class SharedTableContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.Id);
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.Id);
            });
        }

        public static SharedTableContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTableContext>(client));
    }

    private sealed class SharedTableCustomDiscriminatorNameContext(DbContextOptions options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasEmbeddedDiscriminatorName("$kind");

            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.Id);
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.Id);
            });
        }

        public static SharedTableCustomDiscriminatorNameContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTableCustomDiscriminatorNameContext>(client));
    }

    private sealed class SharedTableLateDiscriminatorNameOverrideContext(DbContextOptions options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.Id);
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.Id);
            });

            modelBuilder.HasEmbeddedDiscriminatorName("$kind");
        }

        public static SharedTableLateDiscriminatorNameOverrideContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTableLateDiscriminatorNameOverrideContext>(client));
    }

    private sealed class SharedTableThenSplitContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.Id);
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.Id);
            });

            // Last-wins table mapping should be respected by model-finalizing conventions.
            modelBuilder.Entity<OrderEntity>().ToTable("Orders");
        }

        public static SharedTableThenSplitContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTableThenSplitContext>(client));
    }

    private sealed class SplitThenSharedTableContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("Users");
                b.HasPartitionKey(x => x.Id);
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("Orders");
                b.HasPartitionKey(x => x.Id);
            });

            // Last-wins table mapping should be respected by model-finalizing conventions.
            modelBuilder.Entity<OrderEntity>().ToTable("Users");
        }

        public static SplitThenSharedTableContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SplitThenSharedTableContext>(client));
    }

    [Fact]
    public void SingleTableSingleType_DoesNotConfigureDiscriminatorByConvention()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = SingleTypeContext.Create(client);

        var entityType = context.Model.FindEntityType(typeof(UserEntity))!;
        entityType.FindDiscriminatorProperty().Should().BeNull();
    }

    [Fact]
    public void SharedTableMultipleTypes_UsesDefaultDiscriminatorByConvention()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = SharedTableContext.Create(client);

        var userEntityType = context.Model.FindEntityType(typeof(UserEntity))!;
        var orderEntityType = context.Model.FindEntityType(typeof(OrderEntity))!;

        userEntityType.FindDiscriminatorProperty()!.Name.Should().Be("$type");
        orderEntityType.FindDiscriminatorProperty()!.Name.Should().Be("$type");
        userEntityType.GetDiscriminatorValue().Should().Be("UserEntity");
        orderEntityType.GetDiscriminatorValue().Should().Be("OrderEntity");
    }

    [Fact]
    public void SharedTableMultipleTypes_UsesEmbeddedDiscriminatorNameOverride()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = SharedTableCustomDiscriminatorNameContext.Create(client);

        var userEntityType = context.Model.FindEntityType(typeof(UserEntity))!;
        var orderEntityType = context.Model.FindEntityType(typeof(OrderEntity))!;

        userEntityType.FindDiscriminatorProperty()!.Name.Should().Be("$kind");
        orderEntityType.FindDiscriminatorProperty()!.Name.Should().Be("$kind");
    }

    [Fact]
    public void SharedTableMultipleTypes_UsesLateEmbeddedDiscriminatorNameOverride()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = SharedTableLateDiscriminatorNameOverrideContext.Create(client);

        var userEntityType = context.Model.FindEntityType(typeof(UserEntity))!;
        var orderEntityType = context.Model.FindEntityType(typeof(OrderEntity))!;

        userEntityType.FindDiscriminatorProperty()!.Name.Should().Be("$kind");
        orderEntityType.FindDiscriminatorProperty()!.Name.Should().Be("$kind");
    }

    [Fact]
    public void SharedTableThenSplit_DoesNotConfigureDiscriminatorFromStaleIntermediateState()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = SharedTableThenSplitContext.Create(client);

        var userEntityType = context.Model.FindEntityType(typeof(UserEntity))!;
        var orderEntityType = context.Model.FindEntityType(typeof(OrderEntity))!;

        userEntityType.FindDiscriminatorProperty().Should().BeNull();
        orderEntityType.FindDiscriminatorProperty().Should().BeNull();
    }

    [Fact]
    public void SplitThenSharedTable_ConfiguresDiscriminatorFromFinalTableMapping()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = SplitThenSharedTableContext.Create(client);

        var userEntityType = context.Model.FindEntityType(typeof(UserEntity))!;
        var orderEntityType = context.Model.FindEntityType(typeof(OrderEntity))!;

        userEntityType.FindDiscriminatorProperty()!.Name.Should().Be("$type");
        orderEntityType.FindDiscriminatorProperty()!.Name.Should().Be("$type");
    }
}
