using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.SpecificationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.SaveChanges;

public sealed class SaveChangesSpecificationTests(DynamoContainerFixture containerFixture)
    : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await RecreateTableAsync(containerFixture.Client);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Insert_update_delete_roundtrip()
    {
        await using (var context = SpecSaveChangesContext.Create(containerFixture.Client))
        {
            context.Items.Add(
                new SpecSaveChangesItem
                {
                    Pk = "ITEM#1",
                    Sk = "META",
                    Version = 1,
                    Name = "before",
                    Count = 1
                });
            await context.SaveChangesAsync();
        }

        await using (var context = SpecSaveChangesContext.Create(containerFixture.Client))
        {
            var item =
                (await context.Items.Where(e => e.Pk == "ITEM#1" && e.Sk == "META").ToListAsync())
                .Single();
            item.Name = "after";
            item.Count = 2;
            await context.SaveChangesAsync();
        }

        await using (var context = SpecSaveChangesContext.Create(containerFixture.Client))
        {
            var item =
                (await context.Items.Where(e => e.Pk == "ITEM#1" && e.Sk == "META").ToListAsync())
                .Single();
            item.Name.Should().Be("after");
            item.Count.Should().Be(2);
            context.Items.Remove(item);
            await context.SaveChangesAsync();
        }

        await using var assertContext = SpecSaveChangesContext.Create(containerFixture.Client);
        (await assertContext.Items.Where(e => e.Pk == "ITEM#1" && e.Sk == "META").ToListAsync())
            .Should()
            .BeEmpty();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Concurrency_token_conflict_throws()
    {
        await using (var context = SpecSaveChangesContext.Create(containerFixture.Client))
        {
            context.Items.Add(
                new SpecSaveChangesItem
                {
                    Pk = "ITEM#2",
                    Sk = "META",
                    Version = 1,
                    Name = "original",
                    Count = 1
                });
            await context.SaveChangesAsync();
        }

        await using var first = SpecSaveChangesContext.Create(containerFixture.Client);
        await using var second = SpecSaveChangesContext.Create(containerFixture.Client);
        var firstItem =
            (await first.Items.Where(e => e.Pk == "ITEM#2" && e.Sk == "META").ToListAsync())
            .Single();
        var secondItem =
            (await second.Items.Where(e => e.Pk == "ITEM#2" && e.Sk == "META").ToListAsync())
            .Single();

        firstItem.Version = 2;
        firstItem.Name = "first";
        await first.SaveChangesAsync();

        secondItem.Version = 3;
        secondItem.Name = "second";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    private static async Task RecreateTableAsync(IAmazonDynamoDB client)
    {
        try
        {
            await client.DeleteTableAsync(SpecSaveChangesContext.TableName);
        }
        catch (ResourceNotFoundException) { }

        await client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = SpecSaveChangesContext.TableName,
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

    private sealed class SpecSaveChangesContext(DbContextOptions<SpecSaveChangesContext> options)
        : DbContext(options)
    {
        public const string TableName = "Spec_SaveChanges";

        public DbSet<SpecSaveChangesItem> Items => Set<SpecSaveChangesItem>();

        public static SpecSaveChangesContext Create(IAmazonDynamoDB client)
        {
            var builder = new DbContextOptionsBuilder<SpecSaveChangesContext>();
            builder.UseDynamo(o => o.DynamoDbClient(client));
            builder.ConfigureWarnings(w
                => w
                    .Ignore(DynamoEventId.ScanLikeQueryDetected)
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
            return new SpecSaveChangesContext(builder.Options);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SpecSaveChangesItem>(builder =>
            {
                builder.ToTable(TableName);
                builder.HasPartitionKey(e => e.Pk);
                builder.HasSortKey(e => e.Sk);
                builder.Property(e => e.Version).IsConcurrencyToken();
            });
    }

    private sealed class SpecSaveChangesItem
    {
        public string Pk { get; set; } = null!;

        public string Sk { get; set; } = null!;

        public long Version { get; set; }

        public string Name { get; set; } = null!;

        public int Count { get; set; }
    }
}
