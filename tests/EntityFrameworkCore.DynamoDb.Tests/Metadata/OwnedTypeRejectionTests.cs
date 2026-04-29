using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata;

/// <summary>
///     Verifies that the DynamoDB provider rejects owned entity type configuration at model
///     validation time with a clear error directing users to complex types.
/// </summary>
public class OwnedTypeRejectionTests
{
    private static IAmazonDynamoDB MockClient() => Substitute.For<IAmazonDynamoDB>();

    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

    /// <summary>OwnsOne fluent call throws with a message directing to [ComplexType].</summary>
    [Fact]
    public void OwnsOne_Throws_WithClearMessage()
    {
        var ctx = new OwnsOneContext(BuildOptions<OwnsOneContext>(MockClient()));
        var act = () => ctx.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*owned entity type*not supported*DynamoDB*[ComplexType]*");
    }

    /// <summary>OwnsMany fluent call throws with a message directing to [ComplexType].</summary>
    [Fact]
    public void OwnsMany_Throws_WithClearMessage()
    {
        var ctx = new OwnsManyContext(BuildOptions<OwnsManyContext>(MockClient()));
        var act = () => ctx.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*owned entity type*not supported*DynamoDB*[ComplexType]*");
    }

    private sealed record Tag
    {
        public string Name { get; set; } = null!;
    }

    private sealed record Address
    {
        public string Street { get; set; } = null!;
        public string City { get; set; } = null!;
    }

    private sealed record RootEntity
    {
        public string Pk { get; set; } = null!;
        public Address? Home { get; set; }
        public List<Tag> Tags { get; set; } = [];
    }

    private sealed class OwnsOneContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<RootEntity> Roots => Set<RootEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RootEntity>(b =>
            {
                b.ToTable("roots");
                b.HasPartitionKey(x => x.Pk);
                b.Ignore(x => x.Tags);
                b.OwnsOne(x => x.Home);
            });
    }

    private sealed class OwnsManyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<RootEntity> Roots => Set<RootEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RootEntity>(b =>
            {
                b.ToTable("roots");
                b.HasPartitionKey(x => x.Pk);
                b.Ignore(x => x.Home);
                b.OwnsMany(x => x.Tags);
            });
    }
}
