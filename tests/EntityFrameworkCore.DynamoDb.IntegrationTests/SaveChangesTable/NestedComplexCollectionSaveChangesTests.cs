using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>
///     Integration tests for SaveChanges behaviour when a nullable complex reference is
///     introduced only through a nested complex-collection mutation.
/// </summary>
public class NestedComplexCollectionSaveChangesTests : DynamoTestFixtureBase
{
    private const string TableName = "NestedComplexCollectionItems";

    public NestedComplexCollectionSaveChangesTests(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(TableName, CreateTableAsync);

    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public NestedComplexCollectionDbContext Db
    {
        get
        {
            field ??= new NestedComplexCollectionDbContext(
                CreateOptions<NestedComplexCollectionDbContext>(options
                    => options.DynamoDbClient(Client)));
            return field;
        }
    }

    /// <summary>
    ///     When a previously-missing nullable complex object is introduced only via a nested complex
    ///     collection change, SaveChanges must replace the whole parent map rather than emitting an
    ///     invalid nested-path update against a missing attribute.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        SaveChangesAsync_SetNestedComplexCollectionOnMissingParent_ReplacesWholeParent()
    {
        var item = new NestedComplexCollectionItem { Pk = "ITEM#1", Profile = null };

        Db.Items.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Profile = new NestedProfile { Badges = [new NestedBadge { Label = "vip" }] };

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, CancellationToken);
        raw.Should().NotBeNull();
        raw!.Should().ContainKey("profile");
        raw["profile"].M["badges"].L.Should().HaveCount(1);
        raw["profile"].M["badges"].L[0].M["label"].S.Should().Be("vip");

        AssertSql(
            """
            UPDATE "NestedComplexCollectionItems"
            SET "profile" = ?
            WHERE "pk" = ?
            """);
    }

    private static Task
        CreateTableAsync(IAmazonDynamoDB dynamoDb, CancellationToken cancellationToken)
        => dynamoDb.CreateTableAsync(
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

    private async Task<Dictionary<string, AttributeValue>?> GetItemAsync(
        string pk,
        CancellationToken cancellationToken)
    {
        var response = await Client.GetItemAsync(
            new GetItemRequest
            {
                TableName = TableName,
                Key = new Dictionary<string, AttributeValue> { ["pk"] = new() { S = pk } },
            },
            cancellationToken);

        return response.Item is { Count: > 0 } item ? item : null;
    }

    /// <summary>Represents the root entity used by this regression test.</summary>
    public sealed record NestedComplexCollectionItem
    {
        public string Pk { get; set; } = null!;

        public NestedProfile? Profile { get; set; }
    }

    /// <summary>Represents the nullable parent complex property.</summary>
    public sealed record NestedProfile
    {
        public List<NestedBadge> Badges { get; set; } = [];
    }

    /// <summary>Represents an element in the nested complex collection.</summary>
    public sealed record NestedBadge
    {
        public string Label { get; set; } = null!;
    }

    /// <summary>DbContext used by this regression test.</summary>
    public sealed class NestedComplexCollectionDbContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<NestedComplexCollectionItem> Items => Set<NestedComplexCollectionItem>();

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NestedComplexCollectionItem>(builder =>
            {
                builder.ToTable(TableName);
                builder.HasPartitionKey(x => x.Pk);
                builder.ComplexProperty(
                    x => x.Profile,
                    profile => profile.ComplexCollection(x => x.Badges));
            });
    }
}
