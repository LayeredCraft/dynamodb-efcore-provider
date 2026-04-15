using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

public class SecondaryIndexSuggestOnlyAutoSelectionTests(DynamoContainerFixture fixture)
    : SecondaryIndexTableTestFixture(fixture)
{
    protected override DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.SuggestOnly;

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
