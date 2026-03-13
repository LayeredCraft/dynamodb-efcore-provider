using LayeredCraft.EntityFrameworkCore.DynamoDb.Diagnostics;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure;
using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>
///     Integration tests for suggest-only automatic index selection. Verifies that the provider
///     emits diagnostics for the best index candidate but keeps execution on the base table.
/// </summary>
public class SecondaryIndexSuggestOnlyAutoSelectionTests(SecondaryIndexDynamoFixture fixture)
    : SecondaryIndexTestBase(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     Overrides the base options to enable
    ///     <see cref="DynamoAutomaticIndexSelectionMode.SuggestOnly" /> so diagnostics are emitted without
    ///     rewriting query sources.
    /// </remarks>
    protected override DbContextOptions<SecondaryIndexDbContext> CreateOptions(
        TestPartiQlLoggerFactory loggerFactory)
    {
        var builder =
            new DbContextOptionsBuilder<SecondaryIndexDbContext>(base.CreateOptions(loggerFactory));
        builder.UseDynamo(opt
            => opt.UseAutomaticIndexSelection(DynamoAutomaticIndexSelectionMode.SuggestOnly));
        return builder.Options;
    }

    /// <summary>
    ///     Verifies that a GSI-compatible predicate emits an IDX003 diagnostic in suggest-only mode,
    ///     while the generated PartiQL still targets the base table.
    /// </summary>
    [Fact]
    public async Task SuggestOnly_WhereOnGsiPk_EmitsDiagnosticButStaysOnBaseTable()
    {
        _ = await Db.Orders.Where(o => o.Status == "PENDING").ToListAsync(CancellationToken);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                && e.LogLevel == LogLevel.Information
                && e.Message.Contains(
                    "Index 'ByStatus' on table 'SecondaryIndexOrders' would be selected in Conservative mode."));

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            WHERE "Status" = 'PENDING'
            """);
    }
}
