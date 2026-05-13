using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.SpecificationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Storage;

public sealed class ComplexCollectionsSpecificationTests(DynamoContainerFixture containerFixture)
    : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await RecreateTableAsync(containerFixture.Client);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Complex_type_and_primitive_collection_roundtrip()
    {
        await using (var context = ComplexCollectionsContext.Create(containerFixture.Client))
        {
            context.Items.Add(
                new ComplexCollectionsItem
                {
                    Pk = "COMPLEX#1",
                    Sk = "META",
                    Address = new PostalAddress { City = "London", Country = "UK" },
                    Tags = ["blue", "green"]
                });
            await context.SaveChangesAsync();
        }

        await using var assertContext = ComplexCollectionsContext.Create(containerFixture.Client);
        var item =
            (await assertContext
                .Items
                .Where(e => e.Pk == "COMPLEX#1" && e.Sk == "META")
                .ToListAsync()).Single();
        item.Address.Should().BeEquivalentTo(new PostalAddress { City = "London", Country = "UK" });
        item.Tags.Should().Equal("blue", "green");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Owned_entity_shape_is_explicitly_rejected()
    {
        using var context = UnsupportedOwnedContext.Create(containerFixture.Client);
        var act = () => _ = context.Model;
        act.Should().Throw<InvalidOperationException>().WithMessage("*owned*");
    }

    private static async Task RecreateTableAsync(IAmazonDynamoDB client)
    {
        try
        {
            await client.DeleteTableAsync(ComplexCollectionsContext.TableName);
        }
        catch (ResourceNotFoundException) { }

        await client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = ComplexCollectionsContext.TableName,
                BillingMode = BillingMode.PAY_PER_REQUEST,
                AttributeDefinitions =
                [
                    new AttributeDefinition("pk", ScalarAttributeType.S),
                    new AttributeDefinition("sk", ScalarAttributeType.S)
                ],
                KeySchema =
                [
                    new KeySchemaElement("pk", KeyType.HASH),
                    new KeySchemaElement("sk", KeyType.RANGE)
                ]
            });
    }

    private sealed class ComplexCollectionsContext(
        DbContextOptions<ComplexCollectionsContext> options) : DbContext(options)
    {
        public const string TableName = "Spec_ComplexCollections";

        public DbSet<ComplexCollectionsItem> Items => Set<ComplexCollectionsItem>();

        public static ComplexCollectionsContext Create(IAmazonDynamoDB client)
        {
            var builder = new DbContextOptionsBuilder<ComplexCollectionsContext>();
            builder
                .UseDynamo(o => o.DynamoDbClient(client))
                .ConfigureWarnings(w
                    => w
                        .Ignore(DynamoEventId.ScanLikeQueryDetected)
                        .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
            return new ComplexCollectionsContext(builder.Options);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ComplexCollectionsItem>(b =>
            {
                b.ToTable(TableName);
                b.HasPartitionKey(e => e.Pk);
                b.HasSortKey(e => e.Sk);
                b.ComplexProperty(e => e.Address);
                b.PrimitiveCollection(e => e.Tags);
            });
    }

    private sealed class UnsupportedOwnedContext(DbContextOptions<UnsupportedOwnedContext> options)
        : DbContext(options)
    {
        public DbSet<ComplexCollectionsItem> Items => Set<ComplexCollectionsItem>();

        public static UnsupportedOwnedContext Create(IAmazonDynamoDB client)
        {
            var builder = new DbContextOptionsBuilder<UnsupportedOwnedContext>();
            builder.UseDynamo(o => o.DynamoDbClient(client));
            return new UnsupportedOwnedContext(builder.Options);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ComplexCollectionsItem>(b =>
            {
                b.ToTable(ComplexCollectionsContext.TableName);
                b.HasPartitionKey(e => e.Pk);
                b.HasSortKey(e => e.Sk);
                b.OwnsOne(e => e.Address);
            });
    }

    private sealed class ComplexCollectionsItem
    {
        public string Pk { get; set; } = null!;
        public string Sk { get; set; } = null!;
        public PostalAddress Address { get; set; } = null!;
        public List<string> Tags { get; set; } = [];
    }

    private sealed class PostalAddress
    {
        public string City { get; set; } = null!;
        public string Country { get; set; } = null!;
    }
}
