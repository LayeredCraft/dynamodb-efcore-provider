using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Regression tests for query type-mapping inference at parameter/literal binding.</summary>
public class QueryTypeMappingInferenceTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Captured_short_parameter_uses_property_numeric_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        short value = 7;

        await context
            .Items
            .AllowScan()
            .Where(e => e.ShortValue == value)
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Parameters.Single().N.Should().Be("7");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        Captured_promoted_int_parameter_compared_to_short_property_serializes_as_number()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        var value = 7;

        await context
            .Items
            .AllowScan()
            .Where(e => e.ShortValue == value)
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Parameters.Single().N.Should().Be("7");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        Inline_numeric_literal_compared_to_short_property_serializes_as_number_literal()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);

        await context
            .Items
            .AllowScan()
            .Where(e => e.ShortValue == 7)
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE \"shortValue\" = 7");
        captured.Single().Parameters.Should().BeNullOrEmpty();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Enum_parameter_with_numeric_mapping_serializes_underlying_number()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        var status = NumericStatus.Active;

        await context
            .Items
            .AllowScan()
            .Where(e => e.NumericStatus == status)
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Parameters.Single().N.Should().Be("1");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Converted_property_parameter_uses_property_converter_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        var status = StringStatus.Active;

        await context
            .Items
            .AllowScan()
            .Where(e => e.StringStatus == status)
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Parameters.Single().S.Should().Be(nameof(StringStatus.Active));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Inline_contains_values_use_item_property_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);

        await context
            .Items
            .AllowScan()
            .Where(e => new[] { 1, 2 }.Contains(e.ShortValue))
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE \"shortValue\" IN [1, 2]");
        captured.Single().Parameters.Should().BeNullOrEmpty();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Parameterized_contains_values_use_item_property_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        var values = new[] { 1, 2 };

        await context
            .Items
            .AllowScan()
            .Where(e => values.Contains(e.ShortValue))
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Parameters.Select(p => p.N).Should().Equal("1", "2");
    }

    private static (IAmazonDynamoDB Client, List<ExecuteStatementRequest> Captured) CreateClient()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = new List<ExecuteStatementRequest>();
        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(captured.Add),
                Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [] });

        return (client, captured);
    }

    private enum NumericStatus
    {
        Inactive = 0,
        Active = 1,
    }

    private enum StringStatus
    {
        Inactive,
        Active,
    }

    private sealed class QueryTypeMappingEntity
    {
        public string Id { get; set; } = null!;

        public short ShortValue { get; set; }

        public NumericStatus NumericStatus { get; set; }

        public StringStatus StringStatus { get; set; }
    }

    private sealed class QueryTypeMappingContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<QueryTypeMappingEntity> Items => Set<QueryTypeMappingEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<QueryTypeMappingEntity>(builder =>
            {
                builder.ToTable("QueryTypeMappingItems");
                builder.HasPartitionKey(e => e.Id);
                builder.Property(e => e.Id).HasAttributeName("id");
                builder.Property(e => e.ShortValue).HasAttributeName("shortValue");
                builder.Property(e => e.NumericStatus).HasAttributeName("numericStatus");
                builder
                    .Property(e => e.StringStatus)
                    .HasAttributeName("stringStatus")
                    .HasConversion<string>();
            });

        public static QueryTypeMappingContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<QueryTypeMappingContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}
