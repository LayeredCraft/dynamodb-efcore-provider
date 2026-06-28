using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

/// <summary>Tests SaveChanges planning for table groups and key mapping.</summary>
public class DynamoTableKeySaveChangesTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_AddedDerivedEntity_WritesToBaseTableGroup()
    {
        var (context, captured) = CreateContext();
        context.Add(
            new DerivedDocument { Pk = "DOC#1", Sk = "META#1", Name = "before", Extra = "extra" });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Column order follows EF Core property ordinals. The discriminator shadow property
        // is assigned before base/derived CLR properties in this TPH mapping.
        AssertStatement(
            captured,
            """
            INSERT INTO "Documents"
            VALUE {'pk_attr': ?, 'sk_attr': ?, '$type': ?, 'name': ?, 'extra': ?}
            """,
            "DOC#1",
            "META#1",
            "derived",
            "before",
            "extra");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_ModifiedDerivedEntity_UsesBaseKeyAttributesInWhereClause()
    {
        var (context, captured) = CreateContext();
        var entity = new DerivedDocument
        {
            Pk = "DOC#1", Sk = "META#1", Name = "before", Extra = "extra"
        };
        context.Attach(entity);
        entity.Name = "after";

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        AssertStatement(
            captured,
            """
            UPDATE "Documents"
            SET "name" = ?
            WHERE "pk_attr" = ? AND "sk_attr" = ?
            """,
            "after",
            "DOC#1",
            "META#1");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_DeletedDerivedEntity_UsesBaseKeyAttributesInWhereClause()
    {
        var (context, captured) = CreateContext();
        var entity = new DerivedDocument
        {
            Pk = "DOC#1", Sk = "META#1", Name = "before", Extra = "extra"
        };
        context.Attach(entity);
        context.Remove(entity);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        AssertStatement(
            captured,
            """
            DELETE FROM "Documents"
            WHERE "pk_attr" = ? AND "sk_attr" = ?
            """,
            "DOC#1",
            "META#1");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_ModifiedHasKeyOnlyEntity_UsesFinalKeyAttributesInWhereClause()
    {
        var (context, captured) = CreateContext();
        var entity = new HasKeyDocument { TenantId = "TEN#1", OrderId = "ORD#1", Name = "before" };
        context.Attach(entity);
        entity.Name = "after";

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        AssertStatement(
            captured,
            """
            UPDATE "HasKeyDocuments"
            SET "name" = ?
            WHERE "tenant_id" = ? AND "order_id" = ?
            """,
            "after",
            "TEN#1",
            "ORD#1");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_AddedHasKeyOnlyEntity_IncludesFinalKeyAttributes()
    {
        var (context, captured) = CreateContext();
        context.Add(new HasKeyDocument { TenantId = "TEN#1", OrderId = "ORD#1", Name = "created" });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        AssertStatement(
            captured,
            """
            INSERT INTO "HasKeyDocuments"
            VALUE {'tenant_id': ?, 'order_id': ?, '$type': ?, 'name': ?}
            """,
            "TEN#1",
            "ORD#1",
            nameof(HasKeyDocument),
            "created");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_DeletedHasKeyOnlyEntity_UsesFinalKeyAttributesInWhereClause()
    {
        var (context, captured) = CreateContext();
        var entity = new HasKeyDocument { TenantId = "TEN#1", OrderId = "ORD#1", Name = "before" };
        context.Attach(entity);
        context.Remove(entity);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        AssertStatement(
            captured,
            """
            DELETE FROM "HasKeyDocuments"
            WHERE "tenant_id" = ? AND "order_id" = ?
            """,
            "TEN#1",
            "ORD#1");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_ModifiedSingleHasKeyOnlyEntity_UsesPartitionKeyOnlyWhereClause()
    {
        var (context, captured) = CreateContext();
        var entity = new SingleHasKeyDocument { Id = "DOC#1", Name = "before" };
        context.Attach(entity);
        entity.Name = "after";

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        AssertStatement(
            captured,
            """
            UPDATE "SingleHasKeyDocuments"
            SET "name" = ?
            WHERE "id_attr" = ?
            """,
            "after",
            "DOC#1");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_AddedShadowHasKeyEntity_IncludesShadowKeyAttributes()
    {
        var (context, captured) = CreateContext();
        var entity = new ShadowHasKeyDocument { Name = "created" };
        SetShadowKeys(context.Entry(entity), EntityState.Added);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        AssertStatement(
            captured,
            """
            INSERT INTO "ShadowHasKeyDocuments"
            VALUE {'pk_attr': ?, 'sk_attr': ?, '$type': ?, 'name': ?}
            """,
            "TEN#1",
            "ORD#1",
            nameof(ShadowHasKeyDocument),
            "created");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_ModifiedShadowHasKeyEntity_UsesShadowKeysInWhereClause()
    {
        var (context, captured) = CreateContext();
        var entity = new ShadowHasKeyDocument { Name = "before" };
        SetShadowKeys(context.Entry(entity), EntityState.Unchanged);
        entity.Name = "after";

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        AssertStatement(
            captured,
            """
            UPDATE "ShadowHasKeyDocuments"
            SET "name" = ?
            WHERE "pk_attr" = ? AND "sk_attr" = ?
            """,
            "after",
            "TEN#1",
            "ORD#1");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_DeletedSingleHasKeyOnlyEntity_UsesPartitionKeyOnlyWhereClause()
    {
        var (context, captured) = CreateContext();
        var entity = new SingleHasKeyDocument { Id = "DOC#1", Name = "before" };
        context.Attach(entity);
        context.Remove(entity);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        AssertStatement(
            captured,
            """
            DELETE FROM "SingleHasKeyDocuments"
            WHERE "id_attr" = ?
            """,
            "DOC#1");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_DeletedShadowHasKeyEntity_UsesShadowKeysInWhereClause()
    {
        var (context, captured) = CreateContext();
        var entity = new ShadowHasKeyDocument { Name = "before" };
        SetShadowKeys(context.Entry(entity), EntityState.Unchanged);
        context.Remove(entity);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        AssertStatement(
            captured,
            """
            DELETE FROM "ShadowHasKeyDocuments"
            WHERE "pk_attr" = ? AND "sk_attr" = ?
            """,
            "TEN#1",
            "ORD#1");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void RuntimeModel_StoresTableGroupNameForDerivedAndSharedTableEntities()
    {
        var (context, _) = CreateContext();

        context.Model.FindEntityType(typeof(BaseDocument))!.FindRuntimeAnnotation(
                DynamoAnnotationNames.TableGroupName)!
            .Value
            .Should()
            .Be("Documents");
        context.Model.FindEntityType(typeof(DerivedDocument))!.FindRuntimeAnnotation(
                DynamoAnnotationNames.TableGroupName)!
            .Value
            .Should()
            .Be("Documents");
        context.Model.FindEntityType(typeof(SharedRootA))!.FindRuntimeAnnotation(
                DynamoAnnotationNames.TableGroupName)!
            .Value
            .Should()
            .Be("SharedRoots");
        context.Model.FindEntityType(typeof(SharedRootB))!.FindRuntimeAnnotation(
                DynamoAnnotationNames.TableGroupName)!
            .Value
            .Should()
            .Be("SharedRoots");
    }

    private static (DerivedSaveChangesContext context, List<ParameterizedStatement> captured)
        CreateContext()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = new List<ParameterizedStatement>();
        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(r
                    => captured.Add(
                        new ParameterizedStatement
                        {
                            Statement = r.Statement, Parameters = r.Parameters
                        })),
                Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse());

        client
            .ExecuteTransactionAsync(
                Arg.Do<ExecuteTransactionRequest>(r
                    => captured.AddRange(r.TransactStatements ?? [])),
                Arg.Any<CancellationToken>())
            .Returns(new ExecuteTransactionResponse());

        var options = new DbContextOptionsBuilder<DerivedSaveChangesContext>()
            .UseDynamo(options => options.DynamoDbClient(client))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

        return (new DerivedSaveChangesContext(options), captured);
    }

    private static void AssertStatement(
        IReadOnlyList<ParameterizedStatement> captured,
        string expectedStatement,
        params string[] expectedParameters)
    {
        captured.Should().ContainSingle();
        captured[0].Statement.Should().Be(expectedStatement);
        captured[0].Parameters.Select(GetStringValue).Should().Equal(expectedParameters);
    }

    private static string GetStringValue(AttributeValue value)
        => value.S ?? value.N ?? throw new InvalidOperationException("Expected scalar value.");

    private static void SetShadowKeys(EntityEntry entry, EntityState state)
    {
        entry.Property("PK").CurrentValue = "TEN#1";
        entry.Property("SK").CurrentValue = "ORD#1";
        entry.State = state;
    }

    private abstract class BaseDocument
    {
        public string Pk { get; set; } = null!;
        public string Sk { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class DerivedDocument : BaseDocument
    {
        public string Extra { get; set; } = null!;
    }

    private sealed class HasKeyDocument
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class SingleHasKeyDocument
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class ShadowHasKeyDocument
    {
        public string Name { get; set; } = null!;
    }

    private sealed class SharedRootA
    {
        public string Pk { get; set; } = null!;
        public string Sk { get; set; } = null!;
    }

    private sealed class SharedRootB
    {
        public string Pk { get; set; } = null!;
        public string Sk { get; set; } = null!;
    }

    private sealed class DerivedSaveChangesContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BaseDocument>(b =>
            {
                b.ToTable("Documents");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
                b.Property(x => x.Pk).HasAttributeName("pk_attr");
                b.Property(x => x.Sk).HasAttributeName("sk_attr");
                b.Property(x => x.Name).HasAttributeName("name");
                b
                    .HasDiscriminator<string>("$type")
                    .HasValue<BaseDocument>("base")
                    .HasValue<DerivedDocument>("derived");
            });

            modelBuilder.Entity<DerivedDocument>(b =>
            {
                b.Property(x => x.Extra).HasAttributeName("extra");
            });

            modelBuilder.Entity<HasKeyDocument>(b =>
            {
                b.ToTable("HasKeyDocuments");
                b.HasKey(x => new { x.TenantId, x.OrderId });
                b.Property(x => x.TenantId).HasAttributeName("tenant_id");
                b.Property(x => x.OrderId).HasAttributeName("order_id");
                b.Property(x => x.Name).HasAttributeName("name");
            });

            modelBuilder.Entity<SingleHasKeyDocument>(b =>
            {
                b.ToTable("SingleHasKeyDocuments");
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("id_attr");
                b.Property(x => x.Name).HasAttributeName("name");
            });

            modelBuilder.Entity<ShadowHasKeyDocument>(b =>
            {
                b.ToTable("ShadowHasKeyDocuments");
                b.Property<string>("PK").HasAttributeName("pk_attr");
                b.Property<string>("SK").HasAttributeName("sk_attr");
                b.HasKey("PK", "SK");
                b.Property(x => x.Name).HasAttributeName("name");
            });

            modelBuilder.Entity<SharedRootA>(b =>
            {
                b.ToTable("SharedRoots");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
            });

            modelBuilder.Entity<SharedRootB>(b =>
            {
                b.ToTable("SharedRoots");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
            });
        }
    }
}
