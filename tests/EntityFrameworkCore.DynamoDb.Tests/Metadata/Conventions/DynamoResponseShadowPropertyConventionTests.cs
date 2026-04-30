using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata.Conventions;

/// <summary>
///     Tests for <see cref="DynamoResponseShadowPropertyConvention" /> — verifies that the
///     <c>__executeStatementResponse</c> shadow property is added to root entity types only.
/// </summary>
public class DynamoResponseShadowPropertyConventionTests
{
    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

    // -----------------------------------------------------------------------
    // Root entity types receive the shadow property
    // -----------------------------------------------------------------------

    private sealed record RootEntity
    {
        public string PK { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class RootContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<RootEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RootEntity>(b => b.ToTable("RootTable"));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Convention_AddsShadowProperty_ToRootEntityTypes()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new RootContext(BuildOptions<RootContext>(client));

        var entityType = ctx.Model.FindEntityType(typeof(RootEntity))!;
        var property = entityType.FindProperty("__executeStatementResponse");

        property.Should().NotBeNull();
        property!.ClrType.Should().Be(typeof(ExecuteStatementResponse));
        property.IsShadowProperty().Should().BeTrue();
        property.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
    }

    // -----------------------------------------------------------------------
    // Owned entity types do NOT receive the shadow property
    // -----------------------------------------------------------------------

    private sealed record Address
    {
        public string Street { get; set; } = null!;
    }

    private sealed record OrderEntity
    {
        public string PK { get; set; } = null!;
        public Address? ShippingAddress { get; set; }
    }

    private sealed class OrderContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<OrderEntity> Orders { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("Orders");
                b.OwnsOne(o => o.ShippingAddress);
            });
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Convention_DoesNotAdd_ToOwnedEntityTypes()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new OrderContext(BuildOptions<OrderContext>(client));

        var ownedType = ctx.Model.FindEntityType(typeof(Address));
        ownedType.Should().NotBeNull();
        ownedType!.IsOwned().Should().BeTrue();

        var property = ownedType.FindProperty("__executeStatementResponse");
        property.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Derived entity types do NOT receive the shadow property
    // -----------------------------------------------------------------------

    private abstract class AnimalBase
    {
        public string PK { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class Dog : AnimalBase
    {
        public string Breed { get; set; } = null!;
    }

    private sealed class AnimalContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Dog> Dogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AnimalBase>(b => b.ToTable("Animals"));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Convention_DoesNotAdd_ToDerivedEntityTypes()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new AnimalContext(BuildOptions<AnimalContext>(client));

        var derived = ctx.Model.FindEntityType(typeof(Dog))!;
        derived.BaseType.Should().NotBeNull("Dog is a derived type");

        // The property is defined on the root (AnimalBase), not re-added to the derived type
        var ownDecl =
            derived
                .GetDeclaredProperties()
                .FirstOrDefault(p => p.Name == "__executeStatementResponse");
        ownDecl.Should().BeNull("derived types must not declare a second copy");

        // But it IS accessible via inheritance on the root
        var root = ctx.Model.FindEntityType(typeof(AnimalBase))!;
        root.FindProperty("__executeStatementResponse").Should().NotBeNull();
    }
}
