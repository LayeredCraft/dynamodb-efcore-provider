using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Query.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

public class PrimitiveCollectionContainsTranslationTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task List_property_contains_parameter_translates_to_partiql_contains()
    {
        var (client, captured) = CreateClient();
        await using var context = PrimitiveCollectionContainsContext.Create(client);
        var tag = "alpha";

        await context
            .Items
            .AllowScan()
            .Where(e => e.Tags.Contains(tag))
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE contains(\"tags\", ?)");
        captured.Single().Parameters.Single().S.Should().Be(tag);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Set_property_contains_parameter_translates_to_partiql_contains()
    {
        var (client, captured) = CreateClient();
        await using var context = PrimitiveCollectionContainsContext.Create(client);
        var label = "common";

        await context
            .Items
            .AllowScan()
            .Where(e => e.LabelSet.Contains(label))
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE contains(\"labelSet\", ?)");
        captured.Single().Parameters.Single().S.Should().Be(label);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Number_set_property_contains_inline_constant_uses_element_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = PrimitiveCollectionContainsContext.Create(client);

        await context
            .Items
            .AllowScan()
            .Where(e => e.RatingSet.Contains(2))
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE contains(\"ratingSet\", 2)");
        captured.Single().Parameters.Should().BeNullOrEmpty();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Converted_enum_list_property_contains_uses_element_converter_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = PrimitiveCollectionContainsContext.Create(client);
        var status = CollectionStatus.Active;

        await context
            .Items
            .AllowScan()
            .Where(e => e.Statuses.Contains(status))
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE contains(\"statuses\", ?)");
        captured.Single().Parameters.Single().S.Should().Be(nameof(CollectionStatus.Active));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Scalar_value_converted_collection_contains_remains_unsupported()
    {
        var (client, _) = CreateClient();
        await using var context = PrimitiveCollectionContainsContext.Create(client);
        var status = CollectionStatus.Active;

        var act = () => context
            .Items
            .AllowScan()
            .Where(e => e.CsvStatuses.Contains(status))
            .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{DynamoStrings.ContainsCollectionShapeNotSupported}*");
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

    private enum CollectionStatus
    {
        Inactive,
        Active,
    }

    private sealed class PrimitiveCollectionContainsEntity
    {
        public string Id { get; set; } = null!;

        public List<string> Tags { get; set; } = [];

        public HashSet<string> LabelSet { get; set; } = [];

        public HashSet<int> RatingSet { get; set; } = [];

        public List<CollectionStatus> Statuses { get; set; } = [];

        public List<CollectionStatus> CsvStatuses { get; set; } = [];
    }

    private sealed class PrimitiveCollectionContainsContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<PrimitiveCollectionContainsEntity> Items
            => Set<PrimitiveCollectionContainsEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PrimitiveCollectionContainsEntity>(builder =>
            {
                builder.ToTable("PrimitiveCollectionContainsItems");
                builder.HasPartitionKey(e => e.Id);
                builder.Property(e => e.Id).HasAttributeName("id");
                builder.Property(e => e.Tags).HasAttributeName("tags");
                builder.Property(e => e.LabelSet).HasAttributeName("labelSet");
                builder.Property(e => e.RatingSet).HasAttributeName("ratingSet");
                builder
                    .PrimitiveCollection(e => e.Statuses)
                    .ElementType(e => e.HasConversion<string>());
                builder
                    .Property(e => e.CsvStatuses)
                    .HasAttributeName("csvStatuses")
                    .HasConversion(
                        v => string.Join(',', v),
                        v => v
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(Enum.Parse<CollectionStatus>)
                            .ToList());
            });

        public static PrimitiveCollectionContainsContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<PrimitiveCollectionContainsContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}
