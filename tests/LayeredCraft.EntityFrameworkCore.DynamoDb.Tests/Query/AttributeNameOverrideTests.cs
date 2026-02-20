using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>
///     Tests that <c>HasAttributeName</c> overrides are honored in both PartiQL generation and
///     entity materialization.
/// </summary>
public class AttributeNameOverrideTests
{
    [Fact]
    public async Task PartiQL_UsesAttributeName_NotClrPropertyName()
    {
        // Arrange: capture the statement sent to the DynamoDB client.
        ExecuteStatementRequest? capturedRequest = null;

        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(r => capturedRequest = r),
                Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [] });

        await using var ctx = RenameDbContext.Create(client);

        // Act
        _ = await ctx
            .Entities
            .AsAsyncEnumerable()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert: the generated statement must use the DynamoDB attribute name.
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Statement.Should().Contain("display_name");
        capturedRequest!.Statement.Should().NotContain("FullName");
    }

    [Fact]
    public async Task Materialization_ReadsFromAttributeName_NotClrPropertyName()
    {
        // Arrange: item stored with the DynamoDB attribute name key.
        var item = new Dictionary<string, AttributeValue>
        {
            ["Id"] = new() { S = "e#1" }, ["display_name"] = new() { S = "Ada Lovelace" },
        };

        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [item] });

        await using var ctx = RenameDbContext.Create(client);

        // Act
        var results =
            await ctx
                .Entities
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        // Assert: property is populated from the renamed attribute key.
        results.Should().HaveCount(1);
        results[0].FullName.Should().Be("Ada Lovelace");
    }

    [Fact]
    public async Task Materialization_ItemKeyedByClrName_DoesNotPopulateProperty()
    {
        // Arrange: item stored with the CLR property name instead of the overridden attribute name.
        var item = new Dictionary<string, AttributeValue>
        {
            ["Id"] = new() { S = "e#2" }, ["FullName"] = new() { S = "Should not appear" },
        };

        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [item] });

        await using var ctx = RenameDbContext.Create(client);

        // Act
        var results =
            await ctx
                .Entities
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        // Assert: the CLR-named key is not recognized; property stays null.
        results.Should().HaveCount(1);
        results[0].FullName.Should().BeNull();
    }

    private sealed record RenameEntity
    {
        /// <summary>CLR name is FullName; DynamoDB attribute is "display_name".</summary>
        public string? FullName { get; set; }

        public string Id { get; set; } = null!;
    }

    private sealed class RenameDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<RenameEntity> Entities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RenameEntity>(b =>
            {
                b.ToTable("RenameTestTable");
                b.HasKey(x => x.Id);
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.FullName).HasAttributeName("display_name");
            });

        public static RenameDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<RenameDbContext>()
                    .UseDynamo(o => o.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}
