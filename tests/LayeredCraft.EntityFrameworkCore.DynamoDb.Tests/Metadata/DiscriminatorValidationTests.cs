using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Metadata;

public class DiscriminatorValidationTests
{
    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

    private static IAmazonDynamoDB MockClient() => Substitute.For<IAmazonDynamoDB>();

    private sealed record UserEntity
    {
        public string PK { get; set; } = null!;

        public string SK { get; set; } = null!;
    }

    private sealed record OrderEntity
    {
        public string PK { get; set; } = null!;

        public string SK { get; set; } = null!;
    }

    private sealed class SharedTableValidContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
            });
        }

        public static SharedTableValidContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTableValidContext>(client));
    }

    private sealed class MissingDiscriminatorPropertyContext(DbContextOptions options) : DbContext(
        options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
                b.HasDiscriminator("$type", typeof(string));
                b.Metadata.SetDiscriminatorProperty(null);
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
            });
        }

        public static MissingDiscriminatorPropertyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<MissingDiscriminatorPropertyContext>(client));
    }

    private sealed class MissingDiscriminatorValueContext(DbContextOptions options) : DbContext(
        options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
                b.HasDiscriminator("$type", typeof(string));
                b.Metadata.SetDiscriminatorValue(null);
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
            });
        }

        public static MissingDiscriminatorValueContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<MissingDiscriminatorValueContext>(client));
    }

    private sealed class DuplicateDiscriminatorValueContext(DbContextOptions options) : DbContext(
        options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
                b
                    .HasDiscriminator("$type", typeof(string))
                    .HasValue(typeof(UserEntity), "duplicate");
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
                b
                    .HasDiscriminator("$type", typeof(string))
                    .HasValue(typeof(OrderEntity), "duplicate");
            });
        }

        public static DuplicateDiscriminatorValueContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<DuplicateDiscriminatorValueContext>(client));
    }

    private sealed class DiscriminatorNameMismatchContext(DbContextOptions options) : DbContext(
        options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
                b.Property<string>("$type").HasAttributeName("$kind");
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
            });
        }

        public static DiscriminatorNameMismatchContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<DiscriminatorNameMismatchContext>(client));
    }

    private sealed class DiscriminatorPartitionKeyCollisionContext(DbContextOptions options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
                b.Property(x => x.PK).HasAttributeName("$type");
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
                b.Property(x => x.PK).HasAttributeName("$type");
            });
        }

        public static DiscriminatorPartitionKeyCollisionContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<DiscriminatorPartitionKeyCollisionContext>(client));
    }

    private sealed class DiscriminatorSortKeyCollisionContext(DbContextOptions options) : DbContext(
        options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
                b.Property(x => x.SK).HasAttributeName("$type");
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
                b.Property(x => x.SK).HasAttributeName("$type");
            });
        }

        public static DiscriminatorSortKeyCollisionContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<DiscriminatorSortKeyCollisionContext>(client));
    }

    [Fact]
    public void SharedTableMultipleTypes_WithConventionDiscriminator_IsValid()
    {
        var client = MockClient();
        using var context = SharedTableValidContext.Create(client);

        var act = () => context.Model;

        act.Should().NotThrow();
    }

    [Fact]
    public void SharedTableMultipleTypes_WithMissingDiscriminatorProperty_Throws()
    {
        var client = MockClient();
        using var context = MissingDiscriminatorPropertyContext.Create(client);

        var act = () => context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*does not define a discriminator property*");
    }

    [Fact]
    public void SharedTableMultipleTypes_WithMissingDiscriminatorValue_Throws()
    {
        var client = MockClient();
        using var context = MissingDiscriminatorValueContext.Create(client);

        var act = () => context.Model;

        act.Should().Throw<InvalidOperationException>().WithMessage("*discriminator value*");
    }

    [Fact]
    public void SharedTableMultipleTypes_WithDuplicateDiscriminatorValue_Throws()
    {
        var client = MockClient();
        using var context = DuplicateDiscriminatorValueContext.Create(client);

        var act = () => context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*use duplicate discriminator value*");
    }

    [Fact]
    public void SharedTableMultipleTypes_WithDiscriminatorAttributeNameMismatch_Throws()
    {
        var client = MockClient();
        using var context = DiscriminatorNameMismatchContext.Create(client);

        var act = () => context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*different discriminator attribute names*");
    }

    [Fact]
    public void SharedTableMultipleTypes_WhenDiscriminatorNameCollidesWithPartitionKey_Throws()
    {
        var client = MockClient();
        using var context = DiscriminatorPartitionKeyCollisionContext.Create(client);

        var act = () => context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*collides with the partition key attribute name*");
    }

    [Fact]
    public void SharedTableMultipleTypes_WhenDiscriminatorNameCollidesWithSortKey_Throws()
    {
        var client = MockClient();
        using var context = DiscriminatorSortKeyCollisionContext.Create(client);

        var act = () => context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*collides with the sort key attribute name*");
    }
}
