using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

/// <summary>
///     Tests verifying that the <c>__executeStatementResponse</c> shadow property is correctly
///     excluded from write plans and that <see cref="ExecuteStatementResponse" /> receives a valid
///     no-op type mapping so the model builds without error.
/// </summary>
public class DynamoResponseShadowPropertyTests
{
    // -----------------------------------------------------------------------
    // Shared fixture
    // -----------------------------------------------------------------------

    private sealed record Item
    {
        public string PK { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class ItemContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Item> Items { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Item>(b => b.ToTable("Items"));
    }

    private static ItemContext CreateContext()
        => new(
            new DbContextOptionsBuilder<ItemContext>()
                .UseDynamo()
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

    // -----------------------------------------------------------------------
    // Write plan exclusion
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildPlan_Excludes_ResponseShadowProperty()
    {
        using var ctx = CreateContext();

        var serializer = ctx.GetService<DynamoEntityItemSerializerSource>();

        var entity = new Item { PK = "pk#1", Name = "Test" };
        ctx.Add(entity);

        // BuildItem must not throw and must not contain the shadow property key.
        var entry = (IUpdateEntry)ctx.Entry(entity).GetInfrastructure();
        var item = serializer.BuildItem(entry);

        item.Should().NotContainKey("__executeStatementResponse");
    }

    // -----------------------------------------------------------------------
    // Type mapping: ExecuteStatementResponse shadow property gets a non-null mapping
    // -----------------------------------------------------------------------

    [Fact]
    public void TypeMapping_FindMapping_ReturnsNonNull_ForExecuteStatementResponse()
    {
        using var ctx = CreateContext();

        var entityType = ctx.Model.FindEntityType(typeof(Item))!;
        var property = entityType.FindProperty("__executeStatementResponse")!;

        // FindTypeMapping must return a non-null DynamoTypeMapping — a null return from
        // DynamoTypeMappingSource.FindMapping would have caused model build failure already,
        // but we verify it explicitly here.
        var mapping = property.FindTypeMapping();
        mapping.Should().NotBeNull();
        mapping!.ClrType.Should().Be(typeof(ExecuteStatementResponse));
    }

    // -----------------------------------------------------------------------
    // Model builds without error
    // -----------------------------------------------------------------------

    [Fact]
    public void Model_BuildsWithoutError_WithResponseShadowProperty()
    {
        using var ctx = CreateContext();

        var entityType = ctx.Model.FindEntityType(typeof(Item))!;
        var shadowProperty = entityType.FindProperty("__executeStatementResponse");

        // Convention should have added it; model finalization must not throw.
        shadowProperty.Should().NotBeNull();
    }
}
