using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ComplexTypesTable;

/// <summary>
///     Integration tests verifying that complex properties and complex collections discovered by
///     convention behave correctly without explicit <c>ComplexProperty(...)</c> or
///     <c>ComplexCollection(...)</c> configuration.
/// </summary>
public class ConventionOnlyDiscoveryTests(DynamoContainerFixture fixture)
    : ConventionOnlyComplexTypesTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ModelBuilds_WithConventionDiscoveredComplexPropertyAndCollection()
    {
        var entityType = Db.Model.FindEntityType(typeof(ComplexShapeItem))!;

        var profileProperty = entityType.GetComplexProperties().Single(x => x.Name == "Profile");
        profileProperty.IsCollection.Should().BeFalse();
        profileProperty.ComplexType.ClrType.Should().Be(typeof(Profile));

        var ordersProperty = entityType.GetComplexProperties().Single(x => x.Name == "Orders");
        ordersProperty.IsCollection.Should().BeTrue();
        ordersProperty.ComplexType.ClrType.Should().Be(typeof(Order));

        Db.Model.FindEntityType(typeof(Profile)).Should().BeNull();
        Db.Model.FindEntityType(typeof(Order)).Should().BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ToListAsync_MaterializesConventionDiscoveredComplexMembers()
    {
        var results = await Db.Items.ToListAsync(CancellationToken);

        results.Should().BeEquivalentTo(ComplexTypesItems.Items);

        AssertSql(
            """
            SELECT "pk", "createdAt", "guidValue", "intValue", "ratings", "stringValue", "tags", "orderSnapshots", "orders", "profile"
            FROM "ComplexTypesItems"
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_NestedConventionDiscoveredProperty_TranslatesAndFilters()
    {
        var results =
            await Db
                .Items
                .Where(x => x.Profile!.DisplayName == "Ada")
                .ToListAsync(CancellationToken);

        var expected = ComplexTypesItems.Items.Where(x => x.Profile?.DisplayName == "Ada").ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "createdAt", "guidValue", "intValue", "ratings", "stringValue", "tags", "orderSnapshots", "orders", "profile"
            FROM "ComplexTypesItems"
            WHERE "profile"."displayName" = 'Ada'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Select_ConventionDiscoveredComplexCollection_ProjectsAndMaterializes()
    {
        var results =
            await Db
                .Items
                .AsNoTracking()
                .Select(x => new { x.Pk, x.Orders })
                .ToListAsync(CancellationToken);

        var expected = ComplexTypesItems.Items.Select(x => new { x.Pk, x.Orders }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "orders"
            FROM "ComplexTypesItems"
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Select_ConventionDiscoveredMissingContainer_PropagatesNull()
    {
        var item = ComplexTypesItemMapper.ToItem(ComplexTypesItems.Items[0]);
        item["pk"].S = "CONVENTION#MISSINGPROFILE";
        item.Remove("profile");
        await PutItemAsync(item, CancellationToken);

        try
        {
            var result =
                await Db
                    .Items
                    .Where(x => x.Pk == "CONVENTION#MISSINGPROFILE")
                    .Select(x => new { x.Pk, x.Profile!.Address!.City })
                    .AsAsyncEnumerable()
                    .SingleAsync(CancellationToken);

            result.Pk.Should().Be("CONVENTION#MISSINGPROFILE");
            result.City.Should().BeNull();

            AssertSql(
                """
                SELECT "pk", "profile"
                FROM "ComplexTypesItems"
                WHERE "pk" = 'CONVENTION#MISSINGPROFILE'
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
                        ["pk"] = new() { S = "CONVENTION#MISSINGPROFILE" },
                    },
                },
                CancellationToken);
        }
    }
}
