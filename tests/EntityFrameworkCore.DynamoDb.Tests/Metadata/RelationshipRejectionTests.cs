using System.ComponentModel.DataAnnotations.Schema;
using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata;

/// <summary>Verifies that DynamoDB models reject EF Core relationship metadata.</summary>
public class RelationshipRejectionTests
{
    private static IAmazonDynamoDB MockClient() => Substitute.For<IAmazonDynamoDB>();

    private static DbContextOptions BuildOptions<T>() where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(MockClient()))
            .ConfigureWarnings(w
                => w
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected))
            .Options;

    /// <summary>HasOne/WithOne with explicit foreign key throws provider-specific message.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasOne_WithOne_WithForeignKey_Throws_WithClearMessage()
        => AssertRelationshipRejected<OneToOneContext>();

    /// <summary>HasOne/WithMany with explicit foreign key throws provider-specific message.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasOne_WithMany_WithForeignKey_Throws_WithClearMessage()
        => AssertRelationshipRejected<HasOneWithManyContext>();

    /// <summary>HasMany/WithOne with explicit foreign key throws provider-specific message.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasMany_WithOne_WithForeignKey_Throws_WithClearMessage()
        => AssertRelationshipRejected<HasManyWithOneContext>();

    /// <summary>HasMany/WithMany skip-navigation relationship throws provider-specific message.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasMany_WithMany_Throws_WithClearMessage()
        => AssertRelationshipRejected<ManyToManyContext>();

    /// <summary>ForeignKey attribute-created relationship throws provider-specific message.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ForeignKeyAttribute_Throws_WithClearMessage()
        => AssertRelationshipRejected<ForeignKeyAttributeContext>();

    /// <summary>InverseProperty attribute-created relationship throws provider-specific message.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void InversePropertyAttribute_Throws_WithClearMessage()
        => AssertRelationshipRejected<InversePropertyAttributeContext>();

    /// <summary>Shadow foreign key relationship throws provider-specific message.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ShadowForeignKey_Throws_WithClearMessage()
        => AssertRelationshipRejected<ShadowForeignKeyContext>();

    /// <summary>Relationship without explicit HasForeignKey throws provider-specific message.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void RelationshipWithoutExplicitForeignKey_Throws_WithClearMessage()
        => AssertRelationshipRejected<ConventionalForeignKeyContext>();

    private static void AssertRelationshipRejected<TContext>() where TContext : DbContext
    {
        using var ctx =
            (TContext)Activator.CreateInstance(typeof(TContext), BuildOptions<TContext>())!;
        var act = () => ctx.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("DynamoDB", StringComparison.Ordinal)
                && (ex.Message.Contains("foreign key", StringComparison.OrdinalIgnoreCase)
                    || ex.Message.Contains("relationship", StringComparison.OrdinalIgnoreCase))
                && ex.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase)
                && (ex.Message.Contains("HasOne/HasMany", StringComparison.Ordinal)
                    || ex.Message.Contains(
                        "navigation relationships",
                        StringComparison.OrdinalIgnoreCase))
                && ex.Message.Contains("complex types", StringComparison.OrdinalIgnoreCase));
    }

    private static void ConfigurePrincipal(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Principal>(b =>
        {
            b.ToTable("principals");
            b.HasPartitionKey(e => e.Pk);
        });

    private static void ConfigureDependent(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Dependent>(b =>
        {
            b.ToTable("dependents");
            b.HasPartitionKey(e => e.Pk);
        });

    private sealed class OneToOneContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Principal> Principals => Set<Principal>();
        public DbSet<Dependent> Dependents => Set<Dependent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigurePrincipal(modelBuilder);
            ConfigureDependent(modelBuilder);
            modelBuilder
                .Entity<Dependent>()
                .HasOne(e => e.Principal)
                .WithOne(e => e.SingleDependent)
                .HasForeignKey<Dependent>(e => e.PrincipalPk);
        }
    }

    private sealed class HasOneWithManyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Principal> Principals => Set<Principal>();
        public DbSet<Dependent> Dependents => Set<Dependent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigurePrincipal(modelBuilder);
            ConfigureDependent(modelBuilder);
            modelBuilder
                .Entity<Dependent>()
                .HasOne(e => e.Principal)
                .WithMany(e => e.Dependents)
                .HasForeignKey(e => e.PrincipalPk);
        }
    }

    private sealed class HasManyWithOneContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Principal> Principals => Set<Principal>();
        public DbSet<Dependent> Dependents => Set<Dependent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigurePrincipal(modelBuilder);
            ConfigureDependent(modelBuilder);
            modelBuilder
                .Entity<Principal>()
                .HasMany(e => e.Dependents)
                .WithOne(e => e.Principal)
                .HasForeignKey(e => e.PrincipalPk);
        }
    }

    private sealed class ManyToManyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ManyLeft> Lefts => Set<ManyLeft>();
        public DbSet<ManyRight> Rights => Set<ManyRight>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ManyLeft>(b =>
            {
                b.ToTable("lefts");
                b.HasPartitionKey(e => e.Pk);
            });
            modelBuilder.Entity<ManyRight>(b =>
            {
                b.ToTable("rights");
                b.HasPartitionKey(e => e.Pk);
            });
            modelBuilder.Entity<ManyLeft>().HasMany(e => e.Rights).WithMany(e => e.Lefts);
        }
    }

    private sealed class ForeignKeyAttributeContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ForeignKeyAttributePrincipal> Principals
            => Set<ForeignKeyAttributePrincipal>();

        public DbSet<ForeignKeyAttributeDependent> Dependents
            => Set<ForeignKeyAttributeDependent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ForeignKeyAttributePrincipal>(b =>
            {
                b.ToTable("fk-principals");
                b.HasPartitionKey(e => e.Pk);
            });
            modelBuilder.Entity<ForeignKeyAttributeDependent>(b =>
            {
                b.ToTable("fk-dependents");
                b.HasPartitionKey(e => e.Pk);
            });
        }
    }

    private sealed class InversePropertyAttributeContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<InversePropertyPrincipal> Principals => Set<InversePropertyPrincipal>();
        public DbSet<InversePropertyDependent> Dependents => Set<InversePropertyDependent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InversePropertyPrincipal>(b =>
            {
                b.ToTable("inverse-principals");
                b.HasPartitionKey(e => e.Pk);
            });
            modelBuilder.Entity<InversePropertyDependent>(b =>
            {
                b.ToTable("inverse-dependents");
                b.HasPartitionKey(e => e.Pk);
            });
        }
    }

    private sealed class ShadowForeignKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Principal> Principals => Set<Principal>();
        public DbSet<Dependent> Dependents => Set<Dependent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigurePrincipal(modelBuilder);
            ConfigureDependent(modelBuilder);
            modelBuilder
                .Entity<Dependent>()
                .HasOne(e => e.Principal)
                .WithMany(e => e.Dependents)
                .HasForeignKey("PrincipalShadowPk");
        }
    }

    private sealed class ConventionalForeignKeyContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<Principal> Principals => Set<Principal>();
        public DbSet<Dependent> Dependents => Set<Dependent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigurePrincipal(modelBuilder);
            ConfigureDependent(modelBuilder);
            modelBuilder.Entity<Dependent>().HasOne(e => e.Principal).WithMany(e => e.Dependents);
        }
    }

    private sealed record Principal
    {
        public string Pk { get; set; } = null!;
        public Dependent? SingleDependent { get; set; }
        public List<Dependent> Dependents { get; set; } = [];
    }

    private sealed record Dependent
    {
        public string Pk { get; set; } = null!;
        public string? PrincipalPk { get; set; }
        public Principal? Principal { get; set; }
    }

    private sealed record ManyLeft
    {
        public string Pk { get; set; } = null!;
        public List<ManyRight> Rights { get; set; } = [];
    }

    private sealed record ManyRight
    {
        public string Pk { get; set; } = null!;
        public List<ManyLeft> Lefts { get; set; } = [];
    }

    private sealed record ForeignKeyAttributePrincipal
    {
        public string Pk { get; set; } = null!;
        public List<ForeignKeyAttributeDependent> Dependents { get; set; } = [];
    }

    private sealed record ForeignKeyAttributeDependent
    {
        public string Pk { get; set; } = null!;
        public string PrincipalPk { get; set; } = null!;

        [ForeignKey(nameof(PrincipalPk))]
        public ForeignKeyAttributePrincipal? Principal { get; set; }
    }

    private sealed record InversePropertyPrincipal
    {
        public string Pk { get; set; } = null!;

        [InverseProperty(nameof(InversePropertyDependent.Principal))]
        public List<InversePropertyDependent> Dependents { get; set; } = [];
    }

    private sealed record InversePropertyDependent
    {
        public string Pk { get; set; } = null!;
        public string PrincipalPk { get; set; } = null!;

        [InverseProperty(nameof(InversePropertyPrincipal.Dependents))]
        public InversePropertyPrincipal? Principal { get; set; }
    }
}
