using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

public class DiscriminatorMaterializationSafetyTests
{
    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

    private static IAmazonDynamoDB CreateMockClient(
        IReadOnlyList<Dictionary<string, AttributeValue>> items)
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [.. items] });

        return client;
    }

    private sealed record UserEntity
    {
        public string PK { get; set; } = null!;

        public string SK { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    private sealed record OrderEntity
    {
        public string PK { get; set; } = null!;

        public string SK { get; set; } = null!;

        public string Description { get; set; } = null!;
    }

    private sealed class SharedTableContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<UserEntity> Users => Set<UserEntity>();

        public DbSet<OrderEntity> Orders => Set<OrderEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("app-table");
                b.HasKey(x => new { x.PK, x.SK });
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("app-table");
                b.HasKey(x => new { x.PK, x.SK });
            });
        }

        public static SharedTableContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTableContext>(client));
    }

    [Fact]
    public async Task SharedTableQuery_MissingDiscriminatorAttribute_Throws()
    {
        var client = CreateMockClient(
        [
            new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = "TENANT#1" },
                ["SK"] = new() { S = "USER#1" },
                ["Name"] = new() { S = "Ada" },
            },
        ]);

        await using var context = SharedTableContext.Create(client);

        var act = async ()
            => await context
                .Users
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Required property*$type*");
    }

    [Fact]
    public async Task SharedTableQuery_WrongDiscriminatorValue_Throws()
    {
        var client = CreateMockClient(
        [
            new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = "TENANT#1" },
                ["SK"] = new() { S = "ORDER#1" },
                ["Name"] = new() { S = "Ada" },
                ["$type"] = new() { S = "OrderEntity" },
            },
        ]);

        await using var context = SharedTableContext.Create(client);

        var act = async ()
            => await context
                .Users
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*discriminat*");
    }
}
