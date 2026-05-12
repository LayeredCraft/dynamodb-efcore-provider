using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Metadata;
using EntityFrameworkCore.DynamoDb.Storage.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

/// <summary>Tests DynamoDB table request mapping from EF runtime metadata.</summary>
public sealed class DynamoTableDefinitionBuilderTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void BuildCreateTableRequests_MapsPkOnlyTable()
    {
        using var context = CreateContext<PkOnlyContext>();

        var request = BuildSingleRequest(context);

        request.TableName.Should().Be("PkOnly");
        request.BillingMode.Should().Be(BillingMode.PAY_PER_REQUEST);
        request
            .KeySchema
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeEquivalentTo(new KeySchemaElement("pk", KeyType.HASH));
        request
            .AttributeDefinitions
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeEquivalentTo(new AttributeDefinition("pk", ScalarAttributeType.S));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void BuildCreateTableRequests_MapsPkSkGsiAndLsi()
    {
        using var context = CreateContext<IndexedContext>();

        var request = BuildSingleRequest(context);

        request
            .KeySchema
            .Select(static key => (key.AttributeName, key.KeyType))
            .Should()
            .Equal(("pk", KeyType.HASH), ("sk", KeyType.RANGE));
        request
            .AttributeDefinitions
            .Select(static definition => (definition.AttributeName, definition.AttributeType))
            .Should()
            .Equal(
                ("customer", ScalarAttributeType.S),
                ("pk", ScalarAttributeType.S),
                ("sk", ScalarAttributeType.S),
                ("status", ScalarAttributeType.S));
        request
            .GlobalSecondaryIndexes
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .Match<GlobalSecondaryIndex>(index
                => index.IndexName == "ByCustomer"
                && index.Projection.ProjectionType == ProjectionType.ALL
                && index.KeySchema[0].AttributeName == "customer"
                && index.KeySchema[0].KeyType == KeyType.HASH);
        request
            .LocalSecondaryIndexes
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .Match<LocalSecondaryIndex>(index
                => index.IndexName == "ByStatus"
                && index
                    .KeySchema
                    .Select(static key => key.AttributeName)
                    .SequenceEqual(new[] { "pk", "status" }));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void BuildCreateTableRequests_MapsScalarKeyTypes()
    {
        using var context = CreateContext<ScalarTypeContext>();

        var request = BuildSingleRequest(context);

        request
            .AttributeDefinitions
            .Select(static definition => (definition.AttributeName, definition.AttributeType))
            .Should()
            .Equal(
                ("bytes", ScalarAttributeType.B),
                ("number", ScalarAttributeType.N),
                ("pk", ScalarAttributeType.S));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void BuildCreateTableRequests_RejectsIncludeProjection()
    {
        using var context = CreateContext<IncludeProjectionContext>();

        Action act = () => BuildSingleRequest(context);

        act
            .Should()
            .Throw<NotSupportedException>()
            .WithMessage("*Include projection*non-key projected attributes*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void BuildMissingGlobalSecondaryIndexUpdates_ValidatesExistingAndReturnsMissingGsi()
    {
        using var context = CreateContext<IndexedContext>();
        var table = context.Model.GetDynamoRuntimeTableModel()!.Tables["Indexed"];
        var existing = new TableDescription
        {
            TableName = "Indexed",
            AttributeDefinitions =
            [
                new("customer", ScalarAttributeType.S),
                new("pk", ScalarAttributeType.S),
                new("sk", ScalarAttributeType.S),
                new("status", ScalarAttributeType.S),
                new("extra", ScalarAttributeType.N),
            ],
            KeySchema = [new("pk", KeyType.HASH), new("sk", KeyType.RANGE)],
            LocalSecondaryIndexes =
            [
                new LocalSecondaryIndexDescription
                {
                    IndexName = "ByStatus",
                    KeySchema = [new("pk", KeyType.HASH), new("status", KeyType.RANGE)],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL },
                },
            ],
        };

        var updates =
            DynamoTableDefinitionBuilder.BuildMissingGlobalSecondaryIndexUpdates(table, existing);

        updates.Should().ContainSingle().Which.Create.IndexName.Should().Be("ByCustomer");
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData("pk")]
    [InlineData("sk")]
    [InlineData("customer")]
    [InlineData("status")]
    public void BuildMissingGlobalSecondaryIndexUpdates_RejectsMismatchedKeyAttributeType(
        string attributeName)
    {
        using var context = CreateContext<IndexedContext>();
        var table = context.Model.GetDynamoRuntimeTableModel()!.Tables["Indexed"];
        var existing = new TableDescription
        {
            TableName = "Indexed",
            AttributeDefinitions =
            [
                new("customer", ScalarAttributeType.S),
                new("pk", ScalarAttributeType.S),
                new("sk", ScalarAttributeType.S),
                new("status", ScalarAttributeType.S),
            ],
            KeySchema = [new("pk", KeyType.HASH), new("sk", KeyType.RANGE)],
            GlobalSecondaryIndexes =
            [
                new GlobalSecondaryIndexDescription
                {
                    IndexName = "ByCustomer",
                    KeySchema = [new("customer", KeyType.HASH)],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL },
                },
            ],
            LocalSecondaryIndexes =
            [
                new LocalSecondaryIndexDescription
                {
                    IndexName = "ByStatus",
                    KeySchema = [new("pk", KeyType.HASH), new("status", KeyType.RANGE)],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL },
                },
            ],
        };
        existing.AttributeDefinitions
            .Single(definition => definition.AttributeName == attributeName)
            .AttributeType = ScalarAttributeType.N;

        Action act = ()
            => DynamoTableDefinitionBuilder
                .BuildMissingGlobalSecondaryIndexUpdates(table, existing);

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage($"*key attribute definition for '{attributeName}'*does not match*");
    }

    private static TContext CreateContext<TContext>() where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseDynamo()
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

        return (TContext)Activator.CreateInstance(typeof(TContext), options)!;
    }

    private static CreateTableRequest BuildSingleRequest(DbContext context)
        => DynamoTableDefinitionBuilder
            .BuildCreateTableRequests(context.Model.GetDynamoRuntimeTableModel()!)
            .Should()
            .ContainSingle()
            .Subject;

    private sealed class PkOnlyContext(DbContextOptions<PkOnlyContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PkOnlyEntity>(entity =>
            {
                entity.ToTable("PkOnly");
                entity.Property(x => x.Id).HasAttributeName("pk");
                entity.HasPartitionKey(x => x.Id);
            });
    }

    private sealed class IndexedContext(DbContextOptions<IndexedContext> options) : DbContext(
        options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IndexedEntity>(entity =>
            {
                entity.ToTable("Indexed");
                entity.Property(x => x.Id).HasAttributeName("pk");
                entity.Property(x => x.Sort).HasAttributeName("sk");
                entity.Property(x => x.Customer).HasAttributeName("customer");
                entity.Property(x => x.Status).HasAttributeName("status");
                entity.HasPartitionKey(x => x.Id);
                entity.HasSortKey(x => x.Sort);
                entity.HasGlobalSecondaryIndex("ByCustomer", x => x.Customer);
                entity.HasLocalSecondaryIndex("ByStatus", x => x.Status);
            });
    }

    private sealed class ScalarTypeContext(DbContextOptions<ScalarTypeContext> options) : DbContext(
        options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ScalarTypeEntity>(entity =>
            {
                entity.ToTable("Scalars");
                entity.Property(x => x.Id).HasAttributeName("pk");
                entity.Property(x => x.Number).HasAttributeName("number");
                entity.Property(x => x.Bytes).HasAttributeName("bytes");
                entity.HasPartitionKey(x => x.Id);
                entity.HasGlobalSecondaryIndex("ByNumber", x => x.Number);
                entity.HasGlobalSecondaryIndex("ByBytes", x => x.Bytes);
            });
    }

    private sealed class IncludeProjectionContext(
        DbContextOptions<IncludeProjectionContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IndexedEntity>(entity =>
            {
                entity.ToTable("IncludeProjection");
                entity.Property(x => x.Id).HasAttributeName("pk");
                entity.Property(x => x.Customer).HasAttributeName("customer");
                entity.HasPartitionKey(x => x.Id);
                entity
                    .HasGlobalSecondaryIndex("ByCustomer", x => x.Customer)
                    .IndexBuilder
                    .Metadata
                    .SetSecondaryIndexProjectionType(DynamoSecondaryIndexProjectionType.Include);
            });
    }

    private sealed class PkOnlyEntity
    {
        public string Id { get; set; } = null!;
    }

    private sealed class IndexedEntity
    {
        public string Id { get; set; } = null!;

        public string Sort { get; set; } = null!;

        public string Customer { get; set; } = null!;

        public string Status { get; set; } = null!;
    }

    private sealed class ScalarTypeEntity
    {
        public string Id { get; set; } = null!;

        public int Number { get; set; }

        public byte[] Bytes { get; set; } = null!;
    }
}
