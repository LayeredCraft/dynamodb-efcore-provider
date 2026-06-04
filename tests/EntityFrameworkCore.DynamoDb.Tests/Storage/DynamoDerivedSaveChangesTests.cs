using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

/// <summary>Tests SaveChanges planning for derived entities mapped to shared DynamoDB tables.</summary>
public class DynamoDerivedSaveChangesTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_AddedDerivedEntity_WritesToBaseTableGroup()
    {
        var (context, captured) = CreateContext();
        context.Add(
            new DerivedDocument { Pk = "DOC#1", Sk = "META#1", Name = "before", Extra = "extra" });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        captured.Should().ContainSingle();
        captured[0]
            .Statement
            .Should()
            .StartWith(
                """
                INSERT INTO "Documents"
                """);
        captured[0].Statement.Should().Contain("'pk_attr': ?");
        captured[0].Statement.Should().Contain("'sk_attr': ?");
        captured[0].Statement.Should().Contain("'name': ?");
        captured[0].Statement.Should().Contain("'$type': ?");
        captured[0].Statement.Should().Contain("'extra': ?");
        captured[0]
            .Parameters
            .Select(GetStringValue)
            .Should()
            .Contain(["DOC#1", "META#1", "before", "derived", "extra"]);
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

        captured.Should().ContainSingle();
        captured[0]
            .Statement
            .Should()
            .Be(
                """
                UPDATE "Documents"
                SET "name" = ?
                WHERE "pk_attr" = ? AND "sk_attr" = ?
                """);
        captured[0].Parameters.Select(GetStringValue).Should().Equal("after", "DOC#1", "META#1");
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

        captured.Should().ContainSingle();
        captured[0]
            .Statement
            .Should()
            .Be(
                """
                DELETE FROM "Documents"
                WHERE "pk_attr" = ? AND "sk_attr" = ?
                """);
        captured[0].Parameters.Select(GetStringValue).Should().Equal("DOC#1", "META#1");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_ModifiedHasKeyOnlyEntity_UsesFinalKeyAttributesInWhereClause()
    {
        var (context, captured) = CreateContext();
        var entity = new HasKeyDocument { TenantId = "TEN#1", OrderId = "ORD#1", Name = "before" };
        context.Attach(entity);
        entity.Name = "after";

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        captured.Should().ContainSingle();
        captured[0]
            .Statement
            .Should()
            .Be(
                """
                UPDATE "HasKeyDocuments"
                SET "name" = ?
                WHERE "tenant_id" = ? AND "order_id" = ?
                """);
        captured[0].Parameters.Select(GetStringValue).Should().Equal("after", "TEN#1", "ORD#1");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_AddedHasKeyOnlyEntity_IncludesFinalKeyAttributes()
    {
        var (context, captured) = CreateContext();
        context.Add(new HasKeyDocument { TenantId = "TEN#1", OrderId = "ORD#1", Name = "created" });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        captured.Should().ContainSingle();
        captured[0].Statement.Should().StartWith("INSERT INTO \"HasKeyDocuments\"");
        captured[0].Statement.Should().Contain("'tenant_id': ?");
        captured[0].Statement.Should().Contain("'order_id': ?");
        captured[0]
            .Parameters
            .Select(GetStringValue)
            .Should()
            .Contain(["TEN#1", "ORD#1", "created"]);
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

    private static string GetStringValue(AttributeValue value)
        => value.S ?? value.N ?? throw new InvalidOperationException("Expected scalar value.");

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
