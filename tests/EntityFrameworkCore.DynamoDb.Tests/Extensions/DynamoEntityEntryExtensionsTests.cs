using System.ComponentModel.DataAnnotations.Schema;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Extensions;

public class DynamoEntityEntryExtensionsTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ComplexPropertyMetadata_DoesNotExposeExecuteStatementResponseShadowProperty()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = TestDbContext.Create(client);

        var entityType = context.Model.FindEntityType(typeof(RootEntity))!;
        var complexType = entityType.FindComplexProperty(nameof(RootEntity.Address))!.ComplexType;

        complexType.FindProperty("__executeStatementResponse").Should().BeNull();
    }

    [ComplexType]
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
                b.ComplexProperty(x => x.Address);
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
