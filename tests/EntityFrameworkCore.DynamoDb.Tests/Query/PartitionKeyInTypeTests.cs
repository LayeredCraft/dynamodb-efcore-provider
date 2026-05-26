using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

public class PartitionKeyInTypeTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Numeric_partition_key_in_executes_by_default_and_binds_numbers()
    {
        var (client, captured) = CreateClient();
        await using var context = PartitionKeyInTypeContext.Create(client);
        var ids = new[] { 1, 2 };

        await context
            .NumericItems
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE \"id\" IN [?, ?]");
        captured.Single().Parameters.Select(p => p.N).Should().Equal("1", "2");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Binary_partition_key_in_executes_by_default_and_binds_binary_values()
    {
        var (client, captured) = CreateClient();
        await using var context = PartitionKeyInTypeContext.Create(client);
        var ids = new[] { new byte[] { 1, 2 }, new byte[] { 3, 4 } };

        await context
            .BinaryItems
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE \"id\" IN [?, ?]");
        captured.Single().Parameters.Select(p => p.B.ToArray()).Should().BeEquivalentTo(ids);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Converted_string_partition_key_in_executes_by_default_and_uses_converter()
    {
        var (client, captured) = CreateClient();
        await using var context = PartitionKeyInTypeContext.Create(client);
        var ids = new[] { PartitionKeyKind.Active, PartitionKeyKind.Archived };

        await context
            .ConvertedStringItems
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE \"id\" IN [?, ?]");
        captured.Single().Parameters.Select(p => p.S).Should().Equal("Active", "Archived");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Converted_number_partition_key_in_executes_by_default_and_uses_converter()
    {
        var (client, captured) = CreateClient();
        await using var context = PartitionKeyInTypeContext.Create(client);
        var ids = new[] { new NumericKey(10), new NumericKey(20) };

        await context
            .ConvertedNumberItems
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE \"id\" IN [?, ?]");
        captured.Single().Parameters.Select(p => p.N).Should().Equal("10", "20");
    }

    private static (IAmazonDynamoDB Client, List<ExecuteStatementRequest> Captured) CreateClient()
    {
        var captured = new List<ExecuteStatementRequest>();
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(captured.Add),
                Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [] });

        return (client, captured);
    }

    private enum PartitionKeyKind
    {
        Active,
        Archived
    }

    private readonly record struct NumericKey(int Value);

    private sealed class NumericItem
    {
        public int Id { get; set; }
    }

    private sealed class BinaryItem
    {
        public byte[] Id { get; set; } = null!;
    }

    private sealed class ConvertedStringItem
    {
        public PartitionKeyKind Id { get; set; }
    }

    private sealed class ConvertedNumberItem
    {
        public NumericKey Id { get; set; }
    }

    private sealed class PartitionKeyInTypeContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<NumericItem> NumericItems => Set<NumericItem>();

        public DbSet<BinaryItem> BinaryItems => Set<BinaryItem>();

        public DbSet<ConvertedStringItem> ConvertedStringItems => Set<ConvertedStringItem>();

        public DbSet<ConvertedNumberItem> ConvertedNumberItems => Set<ConvertedNumberItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NumericItem>(b =>
            {
                b.ToTable("NumericPkItems");
                b.HasPartitionKey(e => e.Id);
            });

            modelBuilder.Entity<BinaryItem>(b =>
            {
                b.ToTable("BinaryPkItems");
                b.HasPartitionKey(e => e.Id);
            });

            modelBuilder.Entity<ConvertedStringItem>(b =>
            {
                b.ToTable("ConvertedStringPkItems");
                b.HasPartitionKey(e => e.Id);
                b.Property(e => e.Id).HasConversion<string>();
            });

            modelBuilder.Entity<ConvertedNumberItem>(b =>
            {
                b.ToTable("ConvertedNumberPkItems");
                b.HasPartitionKey(e => e.Id);
                b.Property(e => e.Id).HasConversion(v => v.Value, v => new NumericKey(v));
            });
        }

        public static PartitionKeyInTypeContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<PartitionKeyInTypeContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}
