using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ComplexTypesTable;

/// <summary>
///     Verifies that projecting a primitive collection nested inside an optional complex type
///     reads from the complex map instead of the root item, including when the complex value is
///     null.
/// </summary>
public class PrimitiveCollectionProjectionTests(DynamoContainerFixture fixture)
    : ProjectionComplexTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Select_primitive_collection_inside_optional_complex_materializes()
    {
        await using (var context = CreateContext())
        {
            context.Items.Add(
                new ProjectionComplexItem
                {
                    Pk = "P#1",
                    OptionalComplex = new ProjectionComplex { Name = "A", Ints = [1, 2, 3] }
                });
            await context.SaveChangesAsync(CancellationToken);
        }

        await using var readContext = CreateContext();
        SqlCapture.Clear();
        var result = await readContext
            .Items
            .AsNoTracking()
            .Where(x => x.Pk == "P#1")
            .Select(x => new { x.Pk, x.OptionalComplex!.Ints })
            .SingleAsync(CancellationToken);

        result.Pk.Should().Be("P#1");
        result.Ints.Should().Equal(1, 2, 3);

        AssertSql(
            """
            SELECT "pk", "optionalComplex"
            FROM "ProjectionComplexItems"
            WHERE "pk" = 'P#1'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Select_primitive_collection_inside_null_optional_complex_returns_null()
    {
        await using (var context = CreateContext())
        {
            context.Items.Add(new ProjectionComplexItem { Pk = "P#2", OptionalComplex = null });
            await context.SaveChangesAsync(CancellationToken);
        }

        await using var readContext = CreateContext();
        var result = await readContext
            .Items
            .AsNoTracking()
            .Where(x => x.Pk == "P#2")
            .Select(x => new { x.Pk, x.OptionalComplex!.Ints })
            .SingleAsync(CancellationToken);

        result.Pk.Should().Be("P#2");
        result.Ints.Should().BeNull();
    }
}
