using System.Reflection;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Infrastructure;
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
            .Be("SELECT \"pk\", \"name\"" + Environment.NewLine + "FROM \"ToQueryStringItems\"");
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!, default);
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
                + "SELECT \"pk\", \"name\""
                + Environment.NewLine
                + "FROM \"ToQueryStringItems\""
                + Environment.NewLine
                + "WHERE \"pk\" = ?");
        client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!, default);
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
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["k"] = new() { S = "v" },
                    },
                },
                "{k: 'v'}"
            },
        };

    private static string FormatAttributeValue(AttributeValue value)
        => (string)typeof(DynamoShapedQueryCompilingExpressionVisitor).GetMethod(
            "FormatAttributeValue",
            BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [value])!;

    private sealed class ToQueryStringEntity
    {
        public string Pk { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    private sealed class ToQueryStringDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ToQueryStringEntity> Items => Set<ToQueryStringEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ToQueryStringEntity>(builder =>
            {
                builder.ToTable("ToQueryStringItems");
                builder.HasPartitionKey(e => e.Pk);
            });

        public static ToQueryStringDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<ToQueryStringDbContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}
