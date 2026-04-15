using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventionTable;

/// <summary>
///     Integration tests verifying that per-entity <c>HasAttributeNamingConvention</c> correctly
///     transforms CLR property names in both the PartiQL projection and round-trip read paths, that
///     explicit <c>HasAttributeName</c> overrides win over the convention, and that two entities
///     in the same context can use independent naming conventions.
/// </summary>
public class NamingConventionTests(DynamoContainerFixture fixture)
    : NamingConventionTableTestFixture(fixture)
{
    /// <summary>
    ///     Full round-trip: seed data written under snake_case attribute names is correctly read back
    ///     by the provider into CLR records, including the property stored under an explicit
    ///     <c>HasAttributeName</c> override.
    /// </summary>
    [Fact]
    public async Task SnakeCaseEntity_RoundTrip_ReturnsAllItems()
    {
        var result = await Db.SnakeCaseItems.ToListAsync(CancellationToken);

        result.Should().BeEquivalentTo(NamingConventionData.SnakeCaseItems);
    }

    /// <summary>
    ///     The PartiQL SELECT uses snake_case attribute names for convention-mapped properties and
    ///     the explicit override name (<c>custom_attr</c>) for the property with <c>HasAttributeName</c>.
    /// </summary>
    [Fact]
    public async Task SnakeCaseEntity_EmitsSnakeCaseNamesInPartiQL()
    {
        await Db.SnakeCaseItems.ToListAsync(CancellationToken);

        AssertSql(
            """
            SELECT "pk", "custom_attr", "first_name", "item_count"
            FROM "NamingConventionSnakeCase"
            """);
    }

    /// <summary>
    ///     The kebab-case entity in the same context emits kebab-case attribute names, proving that
    ///     naming conventions are applied independently per entity type.
    /// </summary>
    [Fact]
    public async Task KebabCaseEntity_RoundTrip_ReturnsAllItems()
    {
        var result = await Db.KebabCaseItems.ToListAsync(CancellationToken);

        result.Should().BeEquivalentTo(NamingConventionData.KebabCaseItems);
    }

    /// <summary>
    ///     The kebab-case entity emits kebab-case attribute names in the PartiQL SELECT, proving that
    ///     naming conventions are applied independently per entity type.
    /// </summary>
    [Fact]
    public async Task KebabCaseEntity_EmitsKebabCaseNamesInPartiQL()
    {
        await Db.KebabCaseItems.ToListAsync(CancellationToken);

        AssertSql(
            """
            SELECT "pk", "display-name", "total-count"
            FROM "NamingConventionKebabCase"
            """);
    }
}
