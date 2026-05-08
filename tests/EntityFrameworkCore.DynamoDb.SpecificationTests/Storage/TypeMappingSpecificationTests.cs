using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.SpecificationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Storage;

public sealed class TypeMappingSpecificationTests(DynamoContainerFixture containerFixture)
    : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await RecreateTableAsync(containerFixture.Client);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Scalar_nullable_and_enum_conversion_roundtrip()
    {
        var id = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 5, 8, 12, 30, 0, TimeSpan.Zero);

        await using (var context = TypeMappingContext.Create(containerFixture.Client))
        {
            context.Items.Add(
                new TypeMappingItem
                {
                    Pk = "TYPE#1",
                    Sk = "META",
                    StringValue = "value",
                    IntValue = 42,
                    LongValue = 43,
                    DecimalValue = 12.5m,
                    BoolValue = true,
                    GuidValue = id,
                    DateTimeOffsetValue = at,
                    NullableIntValue = null,
                    Status = TypeMappingStatus.Active
                });
            await context.SaveChangesAsync();
        }

        await using var assertContext = TypeMappingContext.Create(containerFixture.Client);
        var item =
            (await assertContext.Items.Where(e => e.Pk == "TYPE#1" && e.Sk == "META").ToListAsync())
            .Single();
        item.StringValue.Should().Be("value");
        item.IntValue.Should().Be(42);
        item.LongValue.Should().Be(43);
        item.DecimalValue.Should().Be(12.5m);
        item.BoolValue.Should().BeTrue();
        item.GuidValue.Should().Be(id);
        item.DateTimeOffsetValue.Should().Be(at);
        item.NullableIntValue.Should().BeNull();
        item.Status.Should().Be(TypeMappingStatus.Active);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Unsupported_scalar_type_is_rejected_by_model_validation()
    {
        using var context = UnsupportedTypeContext.Create(containerFixture.Client);
        var act = () => _ = context.Model;
        act.Should().Throw<InvalidOperationException>().WithMessage("*Metadata*");
    }

    private static async Task RecreateTableAsync(IAmazonDynamoDB client)
    {
        try
        {
            await client.DeleteTableAsync(TypeMappingContext.TableName);
        }
        catch (ResourceNotFoundException) { }

        await client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = TypeMappingContext.TableName,
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

    private sealed class TypeMappingContext(DbContextOptions<TypeMappingContext> options)
        : DbContext(options)
    {
        public const string TableName = "Spec_TypeMappings";

        public DbSet<TypeMappingItem> Items => Set<TypeMappingItem>();

        public static TypeMappingContext Create(IAmazonDynamoDB client)
        {
            var builder = new DbContextOptionsBuilder<TypeMappingContext>();
            builder.UseDynamo(o => o.DynamoDbClient(client));
            builder.ConfigureWarnings(w
                => w
                    .Ignore(DynamoEventId.ScanLikeQueryDetected)
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
            return new TypeMappingContext(builder.Options);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TypeMappingItem>(b =>
            {
                b.ToTable(TableName);
                b.HasPartitionKey(e => e.Pk);
                b.HasSortKey(e => e.Sk);
                b.Property(e => e.Status).HasConversion<string>();
            });
    }

    private sealed class UnsupportedTypeContext(DbContextOptions<UnsupportedTypeContext> options)
        : DbContext(options)
    {
        public DbSet<UnsupportedTypeItem> Items => Set<UnsupportedTypeItem>();

        public static UnsupportedTypeContext Create(IAmazonDynamoDB client)
        {
            var builder = new DbContextOptionsBuilder<UnsupportedTypeContext>();
            builder.UseDynamo(o => o.DynamoDbClient(client));
            return new UnsupportedTypeContext(builder.Options);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UnsupportedTypeItem>(b =>
            {
                b.ToTable(TypeMappingContext.TableName);
                b.HasPartitionKey(e => e.Pk);
                b.HasSortKey(e => e.Sk);
                b.Property(e => e.Metadata);
            });
    }

    private sealed class TypeMappingItem
    {
        public string Pk { get; set; } = null!;
        public string Sk { get; set; } = null!;
        public string StringValue { get; set; } = null!;
        public int IntValue { get; set; }
        public long LongValue { get; set; }
        public decimal DecimalValue { get; set; }
        public bool BoolValue { get; set; }
        public Guid GuidValue { get; set; }
        public DateTimeOffset DateTimeOffsetValue { get; set; }
        public int? NullableIntValue { get; set; }
        public TypeMappingStatus Status { get; set; }
    }

    private sealed class UnsupportedTypeItem
    {
        public string Pk { get; set; } = null!;
        public string Sk { get; set; } = null!;
        public CustomPayload Metadata { get; set; } = null!;
    }

    private enum TypeMappingStatus
    {
        Unknown,
        Active
    }

    private sealed class CustomPayload { }
}
