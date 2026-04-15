using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

public class SecondaryIndexFirstTests(DynamoContainerFixture fixture)
    : SecondaryIndexTableTestFixture(fixture)
{
    protected override DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.Conservative;

    [Fact]
    public async Task FirstAsync_AutoSelects_ByStatusGsi_PkEquality_SetsLimit1_ReturnsOneItem()
    {
        LoggerFactory.Clear();

        var result = await Db
            .Orders
            .Where(o => o.Status == "PENDING")
            .FirstAsync(CancellationToken);

        result.Status.Should().Be("PENDING");

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'PENDING'
            """);
    }

    [Fact]
    public async Task FirstAsync_AutoSelects_ByStatusGsi_PkAndSkEquality_ReturnsExactItem()
    {
        LoggerFactory.Clear();

        var result = await Db
            .Orders
            .Where(o => o.Status == "PENDING" && o.CreatedAt == "2024-01-10")
            .FirstAsync(CancellationToken);

        var expected =
            OrderItems.Items.Single(o => o.Status == "PENDING" && o.CreatedAt == "2024-01-10");

        result.Should().BeEquivalentTo(expected);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'PENDING' AND "CreatedAt" = '2024-01-10'
            """);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_AutoSelects_ByStatusGsi_ReturnsNullWhenNoMatch()
    {
        var result = await Db
            .Orders
            .Where(o => o.Status == "CANCELLED" && o.CreatedAt == "2024-01-01")
            .FirstOrDefaultAsync(CancellationToken);

        result.Should().BeNull();

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'CANCELLED' AND "CreatedAt" = '2024-01-01'
            """);
    }

    [Fact]
    public async Task FirstAsync_AutoSelects_ByCreatedAtLsi_PkAndSkEquality_ReturnsExactItem()
    {
        LoggerFactory.Clear();

        var result = await Db
            .Orders
            .Where(o => o.CustomerId == "C#1" && o.CreatedAt == "2024-01-10")
            .FirstAsync(CancellationToken);

        var expected =
            OrderItems.Items.Single(o => o.CustomerId == "C#1" && o.CreatedAt == "2024-01-10");

        result.Should().BeEquivalentTo(expected);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByCreatedAt"
            WHERE "CustomerId" = 'C#1' AND "CreatedAt" = '2024-01-10'
            """);
    }

    [Fact]
    public async Task FirstAsync_AutoSelects_ByPriorityLsi_PkAndSkEquality_ReturnsExactItem()
    {
        LoggerFactory.Clear();

        var result = await Db
            .Orders
            .Where(o => o.CustomerId == "C#1" && o.Priority == 5)
            .FirstAsync(CancellationToken);

        var expected = OrderItems.Items.Single(o => o.CustomerId == "C#1" && o.Priority == 5);

        result.Should().BeEquivalentTo(expected);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByPriority"
            WHERE "CustomerId" = 'C#1' AND "Priority" = 5
            """);
    }

    [Fact]
    public async Task
        FirstOrDefaultAsync_AutoSelects_ByStatusGsi_NonKeyFilter_ThrowsTranslationFailure()
    {
        var act = async () => await Db
            .Orders
            .Where(o => o.Status == "PENDING" && o.Region == "US-EAST")
            .FirstOrDefaultAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");
    }

    [Fact]
    public async Task
        FirstOrDefaultAsync_AutoSelects_ByCreatedAtLsi_NonKeyFilter_ThrowsTranslationFailure()
    {
        var act = async () => await Db
            .Orders
            .Where(o
                => o.CustomerId == "C#1" && o.CreatedAt == "2024-01-10" && o.OrderId == "O#001")
            .FirstOrDefaultAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");
    }

    [Fact]
    public async Task FirstAsync_ExplicitHint_ByRegionGsi_PkEquality_SetsLimit1_ReturnsItem()
    {
        LoggerFactory.Clear();

        var result = await Db
            .Orders
            .WithIndex("ByRegion")
            .Where(o => o.Region == "US-WEST")
            .FirstAsync(CancellationToken);

        var expected = OrderItems.Items.Single(o => o.Region == "US-WEST");
        result.Should().BeEquivalentTo(expected);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByRegion"
            WHERE "Region" = 'US-WEST'
            """);
    }
}
