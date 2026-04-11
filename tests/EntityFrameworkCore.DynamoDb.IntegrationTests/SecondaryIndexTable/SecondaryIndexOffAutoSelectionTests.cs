using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>
///     Integration tests for disabled automatic index selection. Verifies that query sources are
///     never rewritten to secondary indexes when mode is set to off.
/// </summary>
public class SecondaryIndexOffAutoSelectionTests(SecondaryIndexDynamoFixture fixture)
    : SecondaryIndexTestBase(fixture)
{
    /// <inheritdoc />
    /// <remarks>Overrides the base options to disable automatic index selection for the class.</remarks>
    protected override DbContextOptions<SecondaryIndexDbContext> CreateOptions(
        TestPartiQlLoggerFactory loggerFactory)
    {
        var builder =
            new DbContextOptionsBuilder<SecondaryIndexDbContext>(base.CreateOptions(loggerFactory));
        builder.UseDynamo(opt
            => opt.UseAutomaticIndexSelection(DynamoAutomaticIndexSelectionMode.Off));
        return builder.Options;
    }

    /// <summary>
    ///     Verifies that automatic index selection remains disabled even when a predicate matches a
    ///     GSI partition key.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Off_WhereOnGsiPk_DoesNotAutoSelect()
    {
        _ = await Db.Orders.Where(o => o.Status == "PENDING").ToListAsync(CancellationToken);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            WHERE "Status" = 'PENDING'
            """);
    }
}
