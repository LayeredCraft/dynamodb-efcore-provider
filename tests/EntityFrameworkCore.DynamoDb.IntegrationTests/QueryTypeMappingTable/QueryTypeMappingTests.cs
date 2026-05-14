using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.QueryTypeMappingTable;

/// <summary>End-to-end coverage for query type-mapping inference and parameter binding.</summary>
public class QueryTypeMappingTests : QueryTypeMappingTestFixture
{
    public QueryTypeMappingTests(DynamoContainerFixture fixture) : base(fixture) { }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Short_parameter_comparison_returns_expected_item()
    {
        short value = 7;

        var result = await Db
            .Items
            .AsNoTracking()
            .Where(item => item.ShortValue == value)
            .Select(item => item.Pk)
            .ToListAsync(CancellationToken);

        result.Should().Equal("ITEM#1");

        AssertSql(
            """
            SELECT "pk"
            FROM "QueryTypeMappingItems"
            WHERE "shortValue" = ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Nullable_short_parameter_comparison_returns_expected_item()
    {
        short? value = 8;

        var result = await Db
            .Items
            .AsNoTracking()
            .Where(item => item.NullableShortValue == value)
            .Select(item => item.Pk)
            .ToListAsync(CancellationToken);

        result.Should().Equal("ITEM#2");

        AssertSql(
            """
            SELECT "pk"
            FROM "QueryTypeMappingItems"
            WHERE "nullableShortValue" = ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Reversed_range_comparison_returns_expected_items()
    {
        short value = 8;

        var result = await Db
            .Items
            .AsNoTracking()
            .Where(item => value <= item.ShortValue)
            .Select(item => item.Pk)
            .ToListAsync(CancellationToken);

        result.Should().BeEquivalentTo(["ITEM#2", "ITEM#3"]);

        AssertSql(
            """
            SELECT "pk"
            FROM "QueryTypeMappingItems"
            WHERE ? <= "shortValue"
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Converted_enum_parameter_comparison_returns_expected_item()
    {
        var status = MappingStatus.Active;

        var result = await Db
            .Items
            .AsNoTracking()
            .Where(item => item.StringStatus == status)
            .Select(item => item.Pk)
            .ToListAsync(CancellationToken);

        result.Should().Equal("ITEM#1");

        AssertSql(
            """
            SELECT "pk"
            FROM "QueryTypeMappingItems"
            WHERE "stringStatus" = ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Nullable_converted_enum_parameter_comparison_returns_expected_item()
    {
        MappingStatus? status = MappingStatus.Active;

        var result = await Db
            .Items
            .AsNoTracking()
            .Where(item => item.NullableStringStatus == status)
            .Select(item => item.Pk)
            .ToListAsync(CancellationToken);

        result.Should().Equal("ITEM#1");

        AssertSql(
            """
            SELECT "pk"
            FROM "QueryTypeMappingItems"
            WHERE "nullableStringStatus" = ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Inline_converted_enum_constant_comparison_returns_expected_item()
    {
        var result = await Db
            .Items
            .AsNoTracking()
            .Where(item => item.StringStatus == MappingStatus.Inactive)
            .Select(item => item.Pk)
            .ToListAsync(CancellationToken);

        result.Should().Equal("ITEM#2");

        AssertSql(
            """
            SELECT "pk"
            FROM "QueryTypeMappingItems"
            WHERE "stringStatus" = 'Inactive'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Parameterized_converted_contains_returns_expected_items()
    {
        var statuses = new[] { MappingStatus.Active, MappingStatus.Pending };

        var result = await Db
            .Items
            .AsNoTracking()
            .Where(item => statuses.Contains(item.StringStatus))
            .Select(item => item.Pk)
            .ToListAsync(CancellationToken);

        result.Should().BeEquivalentTo(["ITEM#1", "ITEM#3"]);

        AssertSql(
            """
            SELECT "pk"
            FROM "QueryTypeMappingItems"
            WHERE "stringStatus" IN [?, ?]
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Complex_converted_property_comparison_returns_expected_item()
    {
        var status = MappingStatus.Pending;

        var result = await Db
            .Items
            .AsNoTracking()
            .Where(item => item.Profile.Status == status)
            .Select(item => item.Pk)
            .ToListAsync(CancellationToken);

        result.Should().Equal("ITEM#3");

        AssertSql(
            """
            SELECT "pk"
            FROM "QueryTypeMappingItems"
            WHERE "profile"."status" = ?
            """);
    }
}

public class QueryTypeMappingTestFixture : DynamoTestFixtureBase
{
    private const string TableName = "QueryTypeMappingItems";

    public QueryTypeMappingTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(TableName, CreateTable);

    public QueryTypeMappingDbContext Db
    {
        get
        {
            field ??= new QueryTypeMappingDbContext(
                CreateOptions<QueryTypeMappingDbContext>(o => o.DynamoDbClient(Client)));
            return field;
        }
    }

    private static async Task CreateTable(
        IAmazonDynamoDB dynamoDb,
        CancellationToken cancellationToken)
    {
        await dynamoDb.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = TableName,
                AttributeDefinitions =
                [
                    new AttributeDefinition
                    {
                        AttributeName = "pk", AttributeType = ScalarAttributeType.S,
                    },
                ],
                KeySchema =
                [
                    new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            },
            cancellationToken);

        await dynamoDb.SeedItemsAsync(TableName, AttributeValues, cancellationToken);
    }

    private static readonly List<Dictionary<string, AttributeValue>> AttributeValues =
    [
        new()
        {
            ["pk"] = new() { S = "ITEM#1" },
            ["shortValue"] = new() { N = "7" },
            ["nullableShortValue"] = new() { NULL = true },
            ["stringStatus"] = new() { S = nameof(MappingStatus.Active) },
            ["nullableStringStatus"] = new() { S = nameof(MappingStatus.Active) },
            ["profile"] =
                new()
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["status"] = new() { S = nameof(MappingStatus.Active) },
                    },
                },
        },
        new()
        {
            ["pk"] = new() { S = "ITEM#2" },
            ["shortValue"] = new() { N = "8" },
            ["nullableShortValue"] = new() { N = "8" },
            ["stringStatus"] = new() { S = nameof(MappingStatus.Inactive) },
            ["nullableStringStatus"] = new() { NULL = true },
            ["profile"] =
                new()
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["status"] = new() { S = nameof(MappingStatus.Inactive) },
                    },
                },
        },
        new()
        {
            ["pk"] = new() { S = "ITEM#3" },
            ["shortValue"] = new() { N = "9" },
            ["nullableShortValue"] = new() { N = "9" },
            ["stringStatus"] = new() { S = nameof(MappingStatus.Pending) },
            ["nullableStringStatus"] = new() { S = nameof(MappingStatus.Pending) },
            ["profile"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["status"] = new() { S = nameof(MappingStatus.Pending) },
                },
            },
        },
    ];
}

public sealed class QueryTypeMappingDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<QueryTypeMappingItem> Items { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<QueryTypeMappingItem>(builder =>
        {
            builder.ToTable("QueryTypeMappingItems");
            builder.HasPartitionKey(item => item.Pk);
            builder.Property(item => item.Pk).HasAttributeName("pk");
            builder.Property(item => item.ShortValue).HasAttributeName("shortValue");
            builder
                .Property(item => item.NullableShortValue)
                .HasAttributeName("nullableShortValue");
            builder
                .Property(item => item.StringStatus)
                .HasAttributeName("stringStatus")
                .HasConversion<string>();
            builder
                .Property(item => item.NullableStringStatus)
                .HasAttributeName("nullableStringStatus")
                .HasConversion<string>();
            builder.ComplexProperty(
                item => item.Profile,
                profileBuilder
                    => profileBuilder.Property(profile => profile.Status).HasConversion<string>());
        });
}

public sealed record QueryTypeMappingItem
{
    public string Pk { get; set; } = null!;

    public short ShortValue { get; set; }

    public short? NullableShortValue { get; set; }

    public MappingStatus StringStatus { get; set; }

    public MappingStatus? NullableStringStatus { get; set; }

    public QueryTypeMappingProfile Profile { get; set; } = new();
}

public sealed record QueryTypeMappingProfile
{
    public MappingStatus Status { get; set; }
}

public enum MappingStatus
{
    Inactive,
    Active,
    Pending,
}
