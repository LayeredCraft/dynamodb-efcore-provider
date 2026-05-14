using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.QueryTypeMappingTable;

/// <summary>End-to-end coverage for query type-mapping inference and parameter binding.</summary>
#pragma warning disable EF9102
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
    public async Task Promoted_int_parameter_compared_to_short_property_returns_expected_item()
    {
        var value = 8;

        var result = await Db
            .Items
            .AsNoTracking()
            .Where(item => item.ShortValue == value)
            .Select(item => item.Pk)
            .ToListAsync(CancellationToken);

        result.Should().Equal("ITEM#2");

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
    public async Task Between_comparison_returns_expected_items()
    {
        var low = 7;
        var high = 8;

        var result = await Db
            .Items
            .AsNoTracking()
            .Where(item => item.ShortValue >= low && item.ShortValue <= high)
            .Select(item => item.Pk)
            .ToListAsync(CancellationToken);

        result.Should().BeEquivalentTo(["ITEM#1", "ITEM#2"]);

        AssertSql(
            """
            SELECT "pk"
            FROM "QueryTypeMappingItems"
            WHERE "shortValue" BETWEEN ? AND ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Numeric_enum_parameter_comparison_returns_expected_item()
    {
        var status = MappingStatus.Active;

        var result = await Db
            .Items
            .AsNoTracking()
            .Where(item => item.NumericStatus == status)
            .Select(item => item.Pk)
            .ToListAsync(CancellationToken);

        result.Should().Equal("ITEM#1");

        AssertSql(
            """
            SELECT "pk"
            FROM "QueryTypeMappingItems"
            WHERE "numericStatus" = ?
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
    public async Task Converted_enum_underlying_parameter_comparison_is_rejected()
    {
        var status = 1;

        var act = () => Db
            .Items
            .AsNoTracking()
            .Where(item => (int)item.StringStatus == status)
            .Select(item => item.Pk)
            .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*value-converted enum*numeric underlying type*");
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
    public async Task Nullable_converted_enum_null_comparison_returns_expected_item()
    {
        MappingStatus? status = null;

        var result = await Db
            .Items
            .AsNoTracking()
            .Where(item => item.NullableStringStatus == status)
            .Select(item => item.Pk)
            .ToListAsync(CancellationToken);

        result.Should().Equal("ITEM#2");

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
    public async Task Parameterized_nullable_converted_contains_returns_expected_items()
    {
        MappingStatus?[] statuses = [MappingStatus.Active, null];

        var result = await Db
            .Items
            .AsNoTracking()
            .Where(item => statuses.Contains(item.NullableStringStatus))
            .Select(item => item.Pk)
            .ToListAsync(CancellationToken);

        result.Should().BeEquivalentTo(["ITEM#1", "ITEM#2"]);

        AssertSql(
            """
            SELECT "pk"
            FROM "QueryTypeMappingItems"
            WHERE "nullableStringStatus" IN [?, ?]
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Materialized_entity_preserves_converted_and_nullable_values()
    {
        var result = await Db
            .Items
            .AsNoTracking()
            .Where(item => item.Pk == "ITEM#2")
            .ToListAsync(CancellationToken);

        var item = result.Should().ContainSingle().Subject;
        item.ShortValue.Should().Be(8);
        item.NullableShortValue.Should().Be(8);
        item.StringStatus.Should().Be(MappingStatus.Inactive);
        item.NumericStatus.Should().Be(MappingStatus.Inactive);
        item.NullableStringStatus.Should().BeNull();
        item.Profile.Status.Should().Be(MappingStatus.Inactive);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Paginated_converted_parameter_query_resumes_with_next_token()
    {
        var statuses =
            new[] { MappingStatus.Active, MappingStatus.Inactive, MappingStatus.Pending };

        var firstPage = await Db
            .Items
            .AsNoTracking()
            .Where(item => statuses.Contains(item.StringStatus))
            .ToPageAsync(2, null, CancellationToken);

        firstPage.Items.Should().HaveCount(2);
        firstPage.NextToken.Should().NotBeNull();

        var secondPage = await Db
            .Items
            .AsNoTracking()
            .Where(item => statuses.Contains(item.StringStatus))
            .ToPageAsync(2, firstPage.NextToken, CancellationToken);

        secondPage.Items.Should().HaveCount(1);
        firstPage
            .Items
            .Concat(secondPage.Items)
            .Select(item => item.Pk)
            .Should()
            .BeEquivalentTo(["ITEM#1", "ITEM#2", "ITEM#3"]);

        AssertSql(
            """
            SELECT "pk", "nullableShortValue", "nullableStringStatus", "numericStatus", "shortValue", "stringStatus", "profile"
            FROM "QueryTypeMappingItems"
            WHERE "stringStatus" IN [?, ?, ?]
            """,
            """
            SELECT "pk", "nullableShortValue", "nullableStringStatus", "numericStatus", "shortValue", "stringStatus", "profile"
            FROM "QueryTypeMappingItems"
            WHERE "stringStatus" IN [?, ?, ?]
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
            ["numericStatus"] = new() { N = "1" },
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
            ["numericStatus"] = new() { N = "0" },
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
            ["numericStatus"] = new() { N = "2" },
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
            builder.Property(item => item.NumericStatus).HasAttributeName("numericStatus");
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

    public MappingStatus NumericStatus { get; set; }

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
