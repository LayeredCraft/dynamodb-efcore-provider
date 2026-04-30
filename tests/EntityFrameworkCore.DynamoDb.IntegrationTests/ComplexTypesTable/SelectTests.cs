using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ComplexTypesTable;

/// <summary>Represents the SelectTests type.</summary>
public class SelectTests(DynamoContainerFixture fixture) : ComplexTypesTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ToListAsync_MaterializesOwnedReferencesAndCollections()
    {
        var results = await Db.Items.ToListAsync(CancellationToken);

        var expected = ComplexTypesItems.Items.ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "createdAt", "guidValue", "intValue", "ratings", "stringValue", "tags", "orderSnapshots", "orders", "profile"
            FROM "ComplexTypesItems"
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Select_NestedOwnedReferenceProjection_MaterializesShape()
    {
        var results =
            await Db.Items.Select(x => new { x.Pk, x.Profile }).ToListAsync(CancellationToken);

        var expected = ComplexTypesItems.Items.Select(x => new { x.Pk, x.Profile }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "profile"
            FROM "ComplexTypesItems"
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Select_NestedOwnedReferencePartialProjection_MaterializesShape()
    {
        var results =
            await Db
                .Items
                .Select(x => new { x.Pk, Age = x.Profile == null ? null : x.Profile.Age })
                .ToListAsync(CancellationToken);

        var expected = ComplexTypesItems.Items.Select(x => new { x.Pk, x.Profile?.Age }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "profile"
            FROM "ComplexTypesItems"
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Select_NestedOwnedCollectionProjection_MaterializesShape()
    {
        var results =
            await Db
                .Items
                .AsNoTracking()
                .Select(x => new { x.Pk, x.Orders, x.OrderSnapshots })
                .ToListAsync(CancellationToken);

        var expected =
            ComplexTypesItems
                .Items
                .OrderBy(x => x.Pk)
                .Select(x => new { x.Pk, x.Orders, x.OrderSnapshots })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "orders", "orderSnapshots"
            FROM "ComplexTypesItems"
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Select_OwnedNavigationChain_IntermediateNull_PropagatesNull()
    {
        var results =
            await Db
                .Items
                .Select(x => new { x.Pk, x.Profile!.Address!.City })
                .ToListAsync(CancellationToken);

        var expected =
            ComplexTypesItems.Items.Select(x => new { x.Pk, x.Profile?.Address?.City }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "profile"
            FROM "ComplexTypesItems"
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Select_OwnedNavigationChain_MissingAttribute_PropagatesNull()
    {
        var item = ComplexTypesItemMapper.ToItem(ComplexTypesItems.Items[0]);
        item["pk"].S = "OWNED#MISSINGPROFILE";
        item.Remove("profile");
        await PutItemAsync(item, CancellationToken);

        try
        {
            var result =
                await Db
                    .Items
                    .Where(x => x.Pk == "OWNED#MISSINGPROFILE")
                    .Select(x => new { x.Pk, x.Profile!.Address!.City })
                    .AsAsyncEnumerable()
                    .SingleAsync(CancellationToken);

            result.Pk.Should().Be("OWNED#MISSINGPROFILE");
            result.City.Should().BeNull();

            AssertSql(
                """
                SELECT "pk", "profile"
                FROM "ComplexTypesItems"
                WHERE "pk" = 'OWNED#MISSINGPROFILE'
                """);
        }
        finally
        {
            await Client.DeleteItemAsync(
                new DeleteItemRequest
                {
                    TableName = ComplexTypesItemTable.TableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new() { S = "OWNED#MISSINGPROFILE" },
                    },
                },
                CancellationToken);
        }
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        ToListAsync_OwnedCollectionElement_WithOptionalOwnedReference_MixedNullMaterializes()
    {
        var item =
            await Db
                .Items
                .Where(x => x.Pk == "OWNED#3")
                .AsAsyncEnumerable()
                .SingleAsync(CancellationToken);

        var withoutPayment = item.Orders.Single(x => x.OrderNumber == "G-500");
        withoutPayment.Payment.Should().BeNull();

        var withPayment = item.Orders.Single(x => x.OrderNumber == "G-501");
        withPayment.Payment.Should().NotBeNull();
        withPayment.Payment!.Provider.Should().Be("stripe");
        withPayment.Payment.Card.Should().NotBeNull();
        withPayment.Payment.Card!.Last4.Should().Be("9999");

        AssertSql(
            """
            SELECT "pk", "createdAt", "guidValue", "intValue", "ratings", "stringValue", "tags", "orderSnapshots", "orders", "profile"
            FROM "ComplexTypesItems"
            WHERE "pk" = 'OWNED#3'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ToListAsync_AsNoTracking_OwnedCollections_MaterializeCorrectly()
    {
        var item =
            await Db
                .Items
                .AsNoTracking()
                .Where(x => x.Pk == "OWNED#3")
                .AsAsyncEnumerable()
                .SingleAsync(CancellationToken);

        item.Orders.Should().HaveCount(2);
        item.Orders.Single(x => x.OrderNumber == "G-500").Lines.Should().HaveCount(1);
        item.Orders.Single(x => x.OrderNumber == "G-501").Lines.Should().HaveCount(2);

        AssertSql(
            """
            SELECT "pk", "createdAt", "guidValue", "intValue", "ratings", "stringValue", "tags", "orderSnapshots", "orders", "profile"
            FROM "ComplexTypesItems"
            WHERE "pk" = 'OWNED#3'
            """);
    }
}
