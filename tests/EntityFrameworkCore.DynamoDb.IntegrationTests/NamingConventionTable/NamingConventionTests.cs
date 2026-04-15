using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventionTable;

/// <summary>
///     Integration tests verifying that <c>HasAttributeNamingConvention</c> correctly transforms
///     CLR property names to DynamoDB attribute names in both read and write paths, and that explicit
///     <c>HasAttributeName</c> overrides are respected.
/// </summary>
public class NamingConventionTests(DynamoContainerFixture fixture)
    : NamingConventionTableTestFixture(fixture)
{
    [Fact]
    public async Task ToListAsync_ReturnsAllItems_WithSnakeCaseConvention()
    {
        var result = await Db.Items.ToListAsync(CancellationToken);

        result.Should().BeEquivalentTo(NamingConventionItems.Items);
    }

    [Fact]
    public async Task ToListAsync_EmitsSnakeCaseAttributeNamesInPartiQL()
    {
        await Db.Items.ToListAsync(CancellationToken);

        AssertSql(
            """
            SELECT "pk", "custom_attr", "first_name", "is_active", "item_count", "last_name"
            FROM "NamingConventionItems"
            """);
    }

    [Fact]
    public async Task Where_OnConventionProperty_EmitsSnakeCaseInPredicate()
    {
        var result = await Db.Items.Where(x => x.IsActive).ToListAsync(CancellationToken);

        var expected = NamingConventionItems.Items.Where(x => x.IsActive).ToList();
        result.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "custom_attr", "first_name", "is_active", "item_count", "last_name"
            FROM "NamingConventionItems"
            WHERE "is_active" = TRUE
            """);
    }

    [Fact]
    public async Task Where_OnMultiWordProperty_EmitsSnakeCaseInPredicate()
    {
        var result =
            await Db.Items.Where(x => x.FirstName == "Alice").ToListAsync(CancellationToken);

        result.Should().ContainSingle(x => x.Pk == "ITEM#1");

        AssertSql(
            """
            SELECT "pk", "custom_attr", "first_name", "is_active", "item_count", "last_name"
            FROM "NamingConventionItems"
            WHERE "first_name" = 'Alice'
            """);
    }

    [Fact]
    public async Task ExplicitHasAttributeName_Override_AppearsInPartiQL_NotSnakeCase()
    {
        // ExplicitOverride maps to "custom_attr", not the convention-derived "explicit_override"
        await Db.Items.ToListAsync(CancellationToken);

        AssertSql(
            """
            SELECT "pk", "custom_attr", "first_name", "is_active", "item_count", "last_name"
            FROM "NamingConventionItems"
            """);
    }

    [Fact]
    public async Task ExplicitHasAttributeName_Override_RoundTrips_Correctly()
    {
        var result = await Db.Items.Where(x => x.Pk == "ITEM#1").ToListAsync(CancellationToken);

        result.Should().ContainSingle();
        result[0].ExplicitOverride.Should().Be("alpha");
    }
}
