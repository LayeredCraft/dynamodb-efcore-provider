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
    public async Task Nullable_short_parameter_uses_property_numeric_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        short? value = 7;

        await context
            .Items
            .AllowScan()
            .Where(e => e.NullableShortValue == value)
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Parameters.Single().N.Should().Be("7");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Reversed_short_parameter_comparison_uses_property_numeric_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        short value = 7;

        await context
            .Items
            .AllowScan()
            .Where(e => value == e.ShortValue)
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Parameters.Single().N.Should().Be("7");
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData("lt")]
    [InlineData("le")]
    [InlineData("gt")]
    [InlineData("ge")]
    public async Task Range_comparison_uses_property_numeric_mapping(string comparison)
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        short value = 7;

        var query = comparison switch
        {
            "lt" => context.Items.AllowScan().Where(e => e.ShortValue < value),
            "le" => context.Items.AllowScan().Where(e => e.ShortValue <= value),
            "gt" => context.Items.AllowScan().Where(e => e.ShortValue > value),
            "ge" => context.Items.AllowScan().Where(e => e.ShortValue >= value),
            _ => throw new InvalidOperationException(),
        };

        await query.ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Parameters.Single().N.Should().Be("7");
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData("lt")]
    [InlineData("le")]
    [InlineData("gt")]
    [InlineData("ge")]
    public async Task Reversed_range_comparison_uses_property_numeric_mapping(string comparison)
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        short value = 7;

        var query = comparison switch
        {
            "lt" => context.Items.AllowScan().Where(e => value < e.ShortValue),
            "le" => context.Items.AllowScan().Where(e => value <= e.ShortValue),
            "gt" => context.Items.AllowScan().Where(e => value > e.ShortValue),
            "ge" => context.Items.AllowScan().Where(e => value >= e.ShortValue),
            _ => throw new InvalidOperationException(),
        };

        await query.ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Parameters.Single().N.Should().Be("7");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Between_comparison_uses_property_numeric_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        var low = 7;
        var high = 9;

        await context
            .Items
            .AllowScan()
            .Where(e => e.ShortValue >= low && e.ShortValue <= high)
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE \"shortValue\" BETWEEN ? AND ?");
        captured.Single().Parameters.Select(p => p.N).Should().Equal("7", "9");
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
    public async Task Enum_underlying_cast_on_converted_property_is_rejected()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        var status = 1;

        var act = () => context
            .Items
            .AllowScan()
            .Where(e => (int)e.StringStatus == status)
            .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*value-converted enum*numeric underlying type*");
        captured.Should().BeEmpty();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Nullable_enum_underlying_cast_on_converted_property_is_rejected()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        var status = 1;

        var act = () => context
            .Items
            .AllowScan()
            .Where(e => e.NullableStringStatus.HasValue
                && (int)e.NullableStringStatus.Value == status)
            .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        captured.Should().BeEmpty();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        Parameterized_contains_enum_underlying_cast_on_converted_property_is_rejected()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        var values = new[] { 1 };

        var act = () => context
            .Items
            .AllowScan()
            .Where(e => values.Contains((int)e.StringStatus))
            .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*value-converted enum*numeric underlying type*");
        captured.Should().BeEmpty();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task List_contains_enum_underlying_cast_on_converted_property_is_rejected()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        var values = new List<int> { 1 };

        var act = () => context
            .Items
            .AllowScan()
            .Where(e => values.Contains((int)e.StringStatus))
            .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*value-converted enum*numeric underlying type*");
        captured.Should().BeEmpty();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        Read_only_list_contains_enum_underlying_cast_on_converted_property_is_rejected()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        IReadOnlyList<int> values = [1];

        var act = () => context
            .Items
            .AllowScan()
            .Where(e => values.Contains((int)e.StringStatus))
            .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*value-converted enum*numeric underlying type*");
        captured.Should().BeEmpty();
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
    public async Task Promoted_byte_enum_parameter_uses_property_converter_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        var status = ByteStringStatus.Active;

        await context
            .Items
            .AllowScan()
            .Where(e => (int)e.ByteStringStatus == (int)status)
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE \"byteStringStatus\" = ?");
        captured.Single().Parameters.Single().S.Should().Be(nameof(ByteStringStatus.Active));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Nullable_converted_enum_parameter_uses_property_converter_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        StringStatus? status = StringStatus.Active;

        await context
            .Items
            .AllowScan()
            .Where(e => e.NullableStringStatus == status)
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Parameters.Single().S.Should().Be(nameof(StringStatus.Active));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Nullable_converted_enum_null_parameter_uses_null_attribute_value()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        StringStatus? status = null;

        await context
            .Items
            .AllowScan()
            .Where(e => e.NullableStringStatus == status)
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Parameters.Single().NULL.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Non_enum_converted_property_parameter_uses_property_converter_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        var code = new QueryTypeMappingCode("A-1");

        await context
            .Items
            .AllowScan()
            .Where(e => e.Code == code)
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE \"code\" = ?");
        captured.Single().Parameters.Single().S.Should().Be("A-1");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Inline_converted_enum_constant_uses_property_converter_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);

        await context
            .Items
            .AllowScan()
            .Where(e => e.StringStatus == StringStatus.Active)
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE \"stringStatus\" = 'Active'");
        captured.Single().Parameters.Should().BeNullOrEmpty();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Inline_string_literal_escapes_single_quotes()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);

        await context
            .Items
            .AllowScan()
            .Where(e => e.Id == "O'Brien")
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE \"id\" = 'O''Brien'");
        captured.Single().Parameters.Should().BeNullOrEmpty();
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
    public async Task Inline_converted_contains_values_use_item_property_converter_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);

        await context
            .Items
            .AllowScan()
            .Where(e => new[] { StringStatus.Active }.Contains(e.StringStatus))
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE \"stringStatus\" IN ['Active']");
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

        captured.Single().Statement.Should().Contain("WHERE \"shortValue\" IN [?, ?]");
        captured.Single().Parameters.Select(p => p.N).Should().Equal("1", "2");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Parameterized_converted_contains_values_use_item_property_converter_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        var values = new[] { StringStatus.Active };

        await context
            .Items
            .AllowScan()
            .Where(e => values.Contains(e.StringStatus))
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE \"stringStatus\" IN [?]");
        captured.Single().Parameters.Select(p => p.S).Should().Equal(nameof(StringStatus.Active));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Parameterized_nullable_converted_contains_values_use_null_attribute_value()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        StringStatus?[] values = [StringStatus.Active, null];

        await context
            .Items
            .AllowScan()
            .Where(e => values.Contains(e.NullableStringStatus))
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Parameters[0].S.Should().Be(nameof(StringStatus.Active));
        captured.Single().Parameters[1].NULL.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Complex_converted_property_parameter_uses_leaf_converter_mapping()
    {
        var (client, captured) = CreateClient();
        await using var context = QueryTypeMappingContext.Create(client);
        var status = StringStatus.Active;

        await context
            .Items
            .AllowScan()
            .Where(e => e.Profile.Status == status)
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Statement.Should().Contain("WHERE \"profile\".\"status\" = ?");
        captured.Single().Parameters.Single().S.Should().Be(nameof(StringStatus.Active));
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

    private enum ByteStringStatus : byte
    {
        Inactive,
        Active,
    }

    private sealed class QueryTypeMappingEntity
    {
        public string Id { get; set; } = null!;

        public short ShortValue { get; set; }

        public short? NullableShortValue { get; set; }

        public NumericStatus NumericStatus { get; set; }

        public StringStatus StringStatus { get; set; }

        public StringStatus? NullableStringStatus { get; set; }

        public ByteStringStatus ByteStringStatus { get; set; }

        public QueryTypeMappingProfile Profile { get; set; } = new();

        public QueryTypeMappingCode Code { get; set; } = new("A-1");
    }

    private sealed record QueryTypeMappingCode(string Value);

    private sealed class QueryTypeMappingProfile
    {
        public StringStatus Status { get; set; }
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
                builder.Property(e => e.NullableShortValue).HasAttributeName("nullableShortValue");
                builder.Property(e => e.NumericStatus).HasAttributeName("numericStatus");
                builder
                    .Property(e => e.Code)
                    .HasAttributeName("code")
                    .HasConversion(code => code.Value, value => new QueryTypeMappingCode(value));
                builder
                    .Property(e => e.StringStatus)
                    .HasAttributeName("stringStatus")
                    .HasConversion<string>();
                builder
                    .Property(e => e.NullableStringStatus)
                    .HasAttributeName("nullableStringStatus")
                    .HasConversion<string>();
                builder
                    .Property(e => e.ByteStringStatus)
                    .HasAttributeName("byteStringStatus")
                    .HasConversion<string>();
                builder.ComplexProperty(
                    e => e.Profile,
                    profileBuilder =>
                    {
                        profileBuilder.Property(p => p.Status).HasConversion<string>();
                    });
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
