using System.Reflection;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Query.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Unit tests for DynamoDB query debug output.</summary>
public class ToQueryStringTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ToQueryString_SimpleQuery_ReturnsPartiQlWithoutExecutingRequest()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = ToQueryStringDbContext.Create(client);

        var queryString = context.Items.AllowScan().ToQueryString();

        queryString
            .Should()
            .Be(
                "SELECT \"pk\", \"$type\", \"name\""
                + Environment.NewLine
                + "FROM \"ToQueryStringItems\"");
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ToQueryString_ParameterizedQuery_IncludesParameterCommentsAndPartiQl()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = ToQueryStringDbContext.Create(client);
        var pk = "tenant'1";

        var queryString = context.Items.Where(e => e.Pk == pk).ToQueryString();

        queryString
            .Should()
            .Be(
                "-- p0='tenant''1'"
                + Environment.NewLine
                + "SELECT \"pk\", \"$type\", \"name\""
                + Environment.NewLine
                + "FROM \"ToQueryStringItems\""
                + Environment.NewLine
                + "WHERE \"pk\" = ?");
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ToQueryString_InlineBinaryConstant_ParameterizesValue()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = ToQueryStringDbContext.Create(client);

        var queryString =
            context.BinaryItems.Where(e => e.Id == new byte[] { 1, 2, 3 }).ToQueryString();

        queryString
            .Should()
            .Be(
                "-- p0=<binary:3 bytes>"
                + Environment.NewLine
                + "SELECT \"id\", \"$type\", \"name\""
                + Environment.NewLine
                + "FROM \"ToQueryStringBinaryItems\""
                + Environment.NewLine
                + "WHERE \"id\" = ?");
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ToQueryString_InlineConvertedBinaryConstant_ParameterizesValue()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = ToQueryStringDbContext.Create(client);

        var queryString = context
            .ConvertedBinaryItems
            .Where(e => e.Id == -1234567890.01M)
            .ToQueryString();

        queryString
            .Should()
            .Be(
                """
                -- p0=<binary:16 bytes>
                SELECT "id", "$type", "name"
                FROM "ToQueryStringConvertedBinaryItems"
                WHERE "id" = ?
                """);
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ToQueryString_InstanceEquals_TranslatesScalarEqualsMethods()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = ToQueryStringDbContext.Create(client);

        var queryString = context
            .EqualsItems
            .AllowScan()
            .Where(e => e.Name.Equals("Leia")
                && e.Count.Equals(7)
                && e.Enabled.Equals(true)
                && e.Status.Equals(ToQueryStringStatus.Active))
            .ToQueryString();

        queryString
            .Should()
            .Be(
                """
                SELECT "pk", "$type", "count", "enabled", "name", "status"
                FROM "ToQueryStringEqualsItems"
                WHERE "name" = 'Leia' AND "count" = 7 AND "enabled" = TRUE AND "status" = 'Active'
                """);
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ToQueryString_ObjectEquals_TranslatesBoxedScalarEqualsMethods()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = ToQueryStringDbContext.Create(client);

        var queryString = context
            .EqualsItems
            .AllowScan()
            .Where(e => Equals(e.Name, "Leia")
                && Equals(e.Count, 7)
                && Equals(e.Enabled, true)
                && Equals(e.Status, ToQueryStringStatus.Active))
            .ToQueryString();

        queryString
            .Should()
            .Be(
                """
                SELECT "pk", "$type", "count", "enabled", "name", "status"
                FROM "ToQueryStringEqualsItems"
                WHERE "name" = 'Leia' AND "count" = 7 AND "enabled" = TRUE AND "status" = 'Active'
                """);
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ToQueryString_EqualsWithParameterLeft_TranslatesScalarEqualsMethod()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = ToQueryStringDbContext.Create(client);
        var name = "Leia";

        var queryString = context
            .EqualsItems
            .AllowScan()
            .Where(e => name.Equals(e.Name))
            .ToQueryString();

        queryString
            .Should()
            .Be(
                """
                -- p0='Leia'
                SELECT "pk", "$type", "count", "enabled", "name", "status"
                FROM "ToQueryStringEqualsItems"
                WHERE ? = "name"
                """);
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ToQueryString_ObjectEqualsNull_UsesNullOrMissingSemantics()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = ToQueryStringDbContext.Create(client);

        var queryString = context
            .EqualsItems
            .AllowScan()
            .Where(e => Equals(e.Name, null))
            .ToQueryString();

        queryString
            .Should()
            .Be(
                """
                SELECT "pk", "$type", "count", "enabled", "name", "status"
                FROM "ToQueryStringEqualsItems"
                WHERE "name" IS NULL OR "name" IS MISSING
                """);
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ToQueryString_ObjectEqualsNullLeft_UsesNullOrMissingSemantics()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = ToQueryStringDbContext.Create(client);

        var queryString = context
            .EqualsItems
            .AllowScan()
            .Where(e => Equals(null, e.Name))
            .ToQueryString();

        queryString
            .Should()
            .Be(
                """
                SELECT "pk", "$type", "count", "enabled", "name", "status"
                FROM "ToQueryStringEqualsItems"
                WHERE "name" IS NULL OR "name" IS MISSING
                """);
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ToQueryString_EqualsNullForNonNullableInt_TranslatesToFalse()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = ToQueryStringDbContext.Create(client);

        var queryString = context
            .EqualsItems
            .AllowScan()
            .Where(e => e.Count.Equals(null))
            .ToQueryString();

        queryString
            .Should()
            .Be(
                """
                SELECT "pk", "$type", "count", "enabled", "name", "status"
                FROM "ToQueryStringEqualsItems"
                WHERE FALSE
                """);
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ToQueryString_ObjectEqualsNullForNonNullableBool_TranslatesToFalse()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = ToQueryStringDbContext.Create(client);

        var queryString = context
            .EqualsItems
            .AllowScan()
            .Where(e => Equals(e.Enabled, null))
            .ToQueryString();

        queryString
            .Should()
            .Be(
                """
                SELECT "pk", "$type", "count", "enabled", "name", "status"
                FROM "ToQueryStringEqualsItems"
                WHERE FALSE
                """);
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ToQueryString_ObjectEqualsNullLeftForNonNullableEnum_TranslatesToFalse()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = ToQueryStringDbContext.Create(client);

        var queryString = context
            .EqualsItems
            .AllowScan()
            .Where(e => Equals(null, e.Status))
            .ToQueryString();

        queryString
            .Should()
            .Be(
                """
                SELECT "pk", "$type", "count", "enabled", "name", "status"
                FROM "ToQueryStringEqualsItems"
                WHERE FALSE
                """);
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ToQueryString_IncompatibleEqualsTypes_TranslatesToFalse()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = ToQueryStringDbContext.Create(client);

        var queryString = context
            .EqualsItems
            .AllowScan()
            .Where(e => Equals(e.Count, "7"))
            .ToQueryString();

        queryString
            .Should()
            .Be(
                """
                SELECT "pk", "$type", "count", "enabled", "name", "status"
                FROM "ToQueryStringEqualsItems"
                WHERE FALSE
                """);
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ToQueryString_ObjectTypedParameterEquals_IsNotTranslated()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = ToQueryStringDbContext.Create(client);
        object status = ToQueryStringStatus.Active;

        var act = () => context
            .EqualsItems
            .AllowScan()
            .Where(e => Equals(e.Status, status))
            .ToQueryString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*could not be translated*");
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [MemberData(nameof(AttributeValues))]
    public void ToQueryString_AttributeValueFormatting_HandlesSdkV4NullCollections(
        AttributeValue value,
        string expected)
        => FormatAttributeValue(value).Should().Be(expected);

    public static TheoryData<AttributeValue, string> AttributeValues()
        => new()
        {
            { new AttributeValue(), "<empty>" },
            { new AttributeValue { SS = ["a", "b'c"] }, "<<'a', 'b''c'>>" },
            { new AttributeValue { NS = ["1", "2"] }, "<<1, 2>>" },
            {
                new AttributeValue { BS = [new MemoryStream([1, 2, 3])] },
                "<<<binary:3 bytes>>>"
            },
            { new AttributeValue { L = [new AttributeValue { S = "x" }] }, "['x']" },
            {
                new AttributeValue
                {
                    M = new Dictionary<string, AttributeValue> { ["k"] = new() { S = "v" } }
                },
                "{k: 'v'}"
            }
        };

    private static string FormatAttributeValue(AttributeValue value)
        => (string)typeof(DynamoShapedQueryCompilingExpressionVisitor).GetMethod(
            "FormatAttributeValue",
            BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [value])!;

    private sealed class ToQueryStringEntity
    {
        public string Name { get; set; } = null!;
        public string Pk { get; set; } = null!;
    }

    private sealed class ToQueryStringBinaryEntity
    {
        public byte[] Id { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    private sealed class ToQueryStringConvertedBinaryEntity
    {
        public decimal Id { get; set; }

        public string Name { get; set; } = null!;
    }

    private sealed class ToQueryStringEqualsEntity
    {
        public int Count { get; set; }

        public bool Enabled { get; set; }

        public string Name { get; set; } = null!;
        public string Pk { get; set; } = null!;

        public ToQueryStringStatus Status { get; set; }
    }

    private enum ToQueryStringStatus
    {
        Inactive,
        Active
    }

    private sealed class ToQueryStringDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ToQueryStringBinaryEntity> BinaryItems => Set<ToQueryStringBinaryEntity>();

        public DbSet<ToQueryStringConvertedBinaryEntity> ConvertedBinaryItems
            => Set<ToQueryStringConvertedBinaryEntity>();

        public DbSet<ToQueryStringEqualsEntity> EqualsItems => Set<ToQueryStringEqualsEntity>();
        public DbSet<ToQueryStringEntity> Items => Set<ToQueryStringEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ToQueryStringEntity>(builder =>
            {
                builder.ToTable("ToQueryStringItems");
                builder.HasPartitionKey(e => e.Pk);
            });

            modelBuilder.Entity<ToQueryStringBinaryEntity>(builder =>
            {
                builder.ToTable("ToQueryStringBinaryItems");
                builder.HasPartitionKey(e => e.Id);
            });

            modelBuilder.Entity<ToQueryStringConvertedBinaryEntity>(builder =>
            {
                builder.ToTable("ToQueryStringConvertedBinaryItems");
                builder.HasPartitionKey(e => e.Id);
                builder.Property(e => e.Id).HasConversion<byte[]>();
            });

            modelBuilder.Entity<ToQueryStringEqualsEntity>(builder =>
            {
                builder.ToTable("ToQueryStringEqualsItems");
                builder.HasPartitionKey(e => e.Pk);
                builder.Property(e => e.Status).HasConversion<string>();
            });
        }

        public static ToQueryStringDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<ToQueryStringDbContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}
