using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata;

/// <summary>Represents the DiscriminatorValidationTests type.</summary>
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
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SK { get; set; } = null!;
    }

    private sealed record OrderEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SK { get; set; } = null!;
    }

    private sealed class SharedTableValidContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
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

        /// <summary>Provides functionality for this member.</summary>
        public static SharedTableValidContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTableValidContext>(client));
    }

    private sealed class MissingDiscriminatorPropertyContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
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

        /// <summary>Provides functionality for this member.</summary>
        public static MissingDiscriminatorPropertyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<MissingDiscriminatorPropertyContext>(client));
    }

    private sealed class MissingDiscriminatorValueContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
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

        /// <summary>Provides functionality for this member.</summary>
        public static MissingDiscriminatorValueContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<MissingDiscriminatorValueContext>(client));
    }

    private sealed class HasNoDiscriminatorContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
                b.HasNoDiscriminator();
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
            });
        }

        /// <summary>Provides functionality for this member.</summary>
        public static HasNoDiscriminatorContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasNoDiscriminatorContext>(client));
    }

    private sealed class HasNoDiscriminatorOnEveryTypeContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
                b.HasNoDiscriminator();
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("App");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
                b.HasNoDiscriminator();
            });
        }

        /// <summary>Provides functionality for this member.</summary>
        public static HasNoDiscriminatorOnEveryTypeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasNoDiscriminatorOnEveryTypeContext>(client));
    }

    private sealed class DuplicateDiscriminatorValueContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
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

        /// <summary>Provides functionality for this member.</summary>
        public static DuplicateDiscriminatorValueContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<DuplicateDiscriminatorValueContext>(client));
    }

    private sealed class DiscriminatorNameMismatchContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
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

        /// <summary>Provides functionality for this member.</summary>
        public static DiscriminatorNameMismatchContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<DiscriminatorNameMismatchContext>(client));
    }

    private sealed class DiscriminatorPartitionKeyCollisionContext(DbContextOptions options)
        : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
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

        /// <summary>Provides functionality for this member.</summary>
        public static DiscriminatorPartitionKeyCollisionContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<DiscriminatorPartitionKeyCollisionContext>(client));
    }

    private sealed class DiscriminatorSortKeyCollisionContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
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

        /// <summary>Provides functionality for this member.</summary>
        public static DiscriminatorSortKeyCollisionContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<DiscriminatorSortKeyCollisionContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void SharedTableMultipleTypes_WithConventionDiscriminator_IsValid()
    {
        var client = MockClient();
        using var context = SharedTableValidContext.Create(client);

        var act = () => context.Model;

        act.Should().NotThrow();
    }

    // MissingDiscriminatorPropertyContext calls HasDiscriminator() explicitly then nulls the
    // property via SetDiscriminatorProperty(null). Because HasDiscriminator() was called with an
    // explicit configuration source, HasExplicitNoDiscriminator() returns true and the convention
    // treats the whole group as opted out — no throw is expected.
    [Fact]
    public void SharedTableMultipleTypes_WithMissingDiscriminatorProperty_RemainsValid()
    {
        var client = MockClient();
        using var context = MissingDiscriminatorPropertyContext.Create(client);

        var act = () => context.Model;

        act.Should().NotThrow();
    }

    [Fact]
    public void SharedTableMultipleTypes_WithHasNoDiscriminator_IsValid()
    {
        var client = MockClient();
        using var context = HasNoDiscriminatorContext.Create(client);

        var act = () => context.Model;

        act.Should().NotThrow();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void SharedTableMultipleTypes_WithHasNoDiscriminatorOnEveryType_IsValid()
    {
        var client = MockClient();
        using var context = HasNoDiscriminatorOnEveryTypeContext.Create(client);

        var act = () => context.Model;

        act.Should().NotThrow();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void SharedTableMultipleTypes_WithMissingDiscriminatorValue_Throws()
    {
        var client = MockClient();
        using var context = MissingDiscriminatorValueContext.Create(client);

        var act = () => context.Model;

        act.Should().Throw<InvalidOperationException>().WithMessage("*discriminator value*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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
