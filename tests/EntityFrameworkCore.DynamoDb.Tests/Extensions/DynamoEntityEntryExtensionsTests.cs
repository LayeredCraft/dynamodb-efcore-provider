using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Extensions;

public class DynamoEntityEntryExtensionsTests
{
    [Fact]
    public void GetExecuteStatementResponse_ReturnsNull_ForOwnedEntry()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = TestDbContext.Create(client);

        var entity = new RootEntity { Pk = "PK#1", Address = new Address { Street = "Main St" } };

        context.Attach(entity);

        var ownedEntry = context.Entry(entity).Reference(x => x.Address).TargetEntry;
        ownedEntry.Should().NotBeNull();

        var response = ownedEntry!.GetExecuteStatementResponse();

        response.Should().BeNull();
    }

    [Fact]
    public void GetExecuteStatementResponse_ReturnsShadowPropertyValue_ForRootEntity()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = TestDbContext.Create(client);

        var entity = new RootEntity { Pk = "PK#1", Address = new Address { Street = "Main St" } };
        context.Attach(entity);

        var expected = new ExecuteStatementResponse();
        context.Entry(entity).Property("__executeStatementResponse").CurrentValue = expected;

        var response = context.Entry(entity).GetExecuteStatementResponse();

        response.Should().BeSameAs(expected);
    }

    private sealed record Address
    {
        public string Street { get; set; } = null!;
    }

    private sealed record RootEntity
    {
        public string Pk { get; set; } = null!;
        public Address? Address { get; set; }
    }

    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<RootEntity> Items => Set<RootEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RootEntity>(b =>
            {
                b.ToTable("TestTable");
                b.HasPartitionKey(x => x.Pk);
                b.OwnsOne(x => x.Address);
            });

        public static TestDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<TestDbContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}
