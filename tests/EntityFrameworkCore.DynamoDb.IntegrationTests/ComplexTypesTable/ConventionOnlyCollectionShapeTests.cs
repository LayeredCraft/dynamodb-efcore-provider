using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ComplexTypesTable;

/// <summary>
///     Integration tests verifying convention-only support for the provider's supported complex
///     collection CLR shapes.
/// </summary>
public class ConventionOnlyCollectionShapeTests(DynamoContainerFixture fixture)
    : ConventionOnlyCollectionShapeTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ModelBuilds_WithSupportedConventionOnlyCollectionShapes()
    {
        var entityType = Db.Model.FindEntityType(typeof(CollectionShapeItem))!;

        AssertComplexCollection(
            entityType,
            nameof(CollectionShapeItem.MutableContacts),
            typeof(List<ShapeContact>));
        AssertComplexCollection(
            entityType,
            nameof(CollectionShapeItem.InterfaceContacts),
            typeof(IList<ShapeContact>));

        Db.Model.FindEntityType(typeof(ShapeContact)).Should().BeNull();
        Db.Model.FindEntityType(typeof(ShapeAddress)).Should().BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveAndLoad_RoundTrips_SupportedConventionOnlyCollectionShapes()
    {
        var entity = CreateEntity("SHAPES#1");

        await Db.AddAsync(entity, CancellationToken);
        await Db.SaveChangesAsync(CancellationToken);
        Db.ChangeTracker.Clear();

        using var readCtx = ConventionOnlyCollectionShapeDbContext.Create(Client);
        var loaded =
            await readCtx
                .Items
                .Where(x => x.Pk == entity.Pk)
                .AsAsyncEnumerable()
                .SingleAsync(CancellationToken);

        loaded.Pk.Should().Be(entity.Pk);
        loaded.MutableContacts.Should().BeEquivalentTo(entity.MutableContacts);
        loaded.InterfaceContacts.Should().BeEquivalentTo(entity.InterfaceContacts);

        loaded.MutableContacts.Should().BeOfType<List<ShapeContact>>();
        loaded.InterfaceContacts.Should().BeAssignableTo<IList<ShapeContact>>();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Select_Projects_SupportedConventionOnlyCollectionShapes()
    {
        var entity = CreateEntity("SHAPES#2");

        await Db.AddAsync(entity, CancellationToken);
        await Db.SaveChangesAsync(CancellationToken);
        Db.ChangeTracker.Clear();

        using var readCtx = ConventionOnlyCollectionShapeDbContext.Create(Client);
        var result =
            await readCtx
                .Items
                .Where(x => x.Pk == entity.Pk)
                .Select(x => new { x.Pk, x.MutableContacts, x.InterfaceContacts })
                .AsAsyncEnumerable()
                .SingleAsync(CancellationToken);

        result.Pk.Should().Be(entity.Pk);
        result.MutableContacts.Should().BeEquivalentTo(entity.MutableContacts);
        result.InterfaceContacts.Should().BeEquivalentTo(entity.InterfaceContacts);

        result.MutableContacts.Should().BeOfType<List<ShapeContact>>();
        result.InterfaceContacts.Should().BeAssignableTo<IList<ShapeContact>>();
    }

    private static void AssertComplexCollection(
        IEntityType entityType,
        string propertyName,
        Type clrType)
    {
        var complexProperty = entityType.GetComplexProperties().Single(x => x.Name == propertyName);
        complexProperty.IsCollection.Should().BeTrue();
        complexProperty.ClrType.Should().Be(clrType);
        complexProperty.ComplexType.ClrType.Should().Be(typeof(ShapeContact));
    }

    private static CollectionShapeItem CreateEntity(string pk)
        => new()
        {
            Pk = pk,
            MutableContacts =
            [
                new ShapeContact
                {
                    Label = "mutable", Address = new ShapeAddress { City = "Seattle" },
                },
            ],
            InterfaceContacts = new List<ShapeContact>
            {
                new() { Label = "ilist", Address = new ShapeAddress { City = "Portland" } },
            },
        };
}
