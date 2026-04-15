using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

public class SecondaryIndexOffAutoSelectionTests(DynamoContainerFixture fixture)
    : SecondaryIndexTableTestFixture(fixture)
{
    protected override DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.Off;

    [Fact]
    public async Task Off_WhereOnGsiPk_DoesNotAutoSelect()
    {
        _ = await Db.Orders.Where(o => o.Status == "PENDING").ToListAsync(CancellationToken);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            WHERE "Status" = 'PENDING'
            """);
    }
}
