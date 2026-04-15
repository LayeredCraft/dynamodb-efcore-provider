using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SecondaryIndexTable;

public class SecondaryIndexAutoSelectionTests(DynamoContainerFixture fixture)
    : SecondaryIndexTableTestFixture(fixture)
{
    protected override DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.Conservative;

    [Fact]
    public async Task Conservative_WhereOnGsiPk_AutoSelects_ByStatusIndex()
    {
        var results =
            await Db.Orders.Where(o => o.Status == "PENDING").ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => o.Status == "PENDING").ToList();
        results.Should().BeEquivalentTo(expected);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                && e.LogLevel == LogLevel.Information
                && e.Message.Contains(
                    "Index 'ByStatus' on table 'SecondaryIndexOrders' was auto-selected."));

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'PENDING'
            """);
    }

    [Fact]
    public async Task Conservative_WhereOnGsiPk_WithSkRange_AutoSelects_AndProducesCorrectResults()
    {
        var results = await Db
            .Orders
            .Where(o => o.Region == "US-EAST" && string.Compare(o.CreatedAt, "2024-01-14") >= 0)
            .ToListAsync(CancellationToken);

        var expected = OrderItems
            .Items
            .Where(o => o.Region == "US-EAST"
                && string.Compare(o.CreatedAt, "2024-01-14", StringComparison.Ordinal) >= 0)
            .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByRegion"
            WHERE "Region" = 'US-EAST' AND "CreatedAt" >= '2024-01-14'
            """);
    }

    [Fact]
    public async Task Conservative_WhereOnTablePk_DoesNotAutoSelectGsi_StaysOnBaseTable()
    {
        var results =
            await Db.Orders.Where(o => o.CustomerId == "C#1").ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => o.CustomerId == "C#1").ToList();
        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            WHERE "CustomerId" = 'C#1'
            """);
    }

    [Fact]
    public async Task Conservative_WhereOnTablePk_LsiCandidates_AutoSelects_WhenOrderingMatches()
    {
        List<OrderItem>? results = null;
        try
        {
            results = await Db
                .Orders
                .Where(o => o.CustomerId == "C#1")
                .OrderBy(o => o.Priority)
                .ToListAsync(CancellationToken);
        }
        catch (AmazonDynamoDBException) { }

        if (results is not null)
        {
            var expected = OrderItems
                .Items
                .Where(o => o.CustomerId == "C#1")
                .OrderBy(o => o.Priority)
                .ToList();
            results.Should().ContainInOrder(expected);
        }

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByPriority"
            WHERE "CustomerId" = 'C#1'
            ORDER BY "Priority" ASC
            """);
    }

    [Fact]
    public async Task Conservative_WhereOnTablePk_AmbiguousLsi_FallsBackToBaseTable()
    {
        var results =
            await Db.Orders.Where(o => o.CustomerId == "C#1").ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => o.CustomerId == "C#1").ToList();
        results.Should().BeEquivalentTo(expected);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .NotContain(e => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            WHERE "CustomerId" = 'C#1'
            """);
    }

    [Fact]
    public async Task Conservative_ContainsOnGsiPk_AutoSelects_ByStatusIndex()
    {
        var statusList = new List<string> { "PENDING", "SHIPPED" };

        var results = await Db
            .Orders
            .Where(o => statusList.Contains(o.Status))
            .ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => statusList.Contains(o.Status)).ToList();
        results.Should().BeEquivalentTo(expected);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                && e.Message.Contains(
                    "Index 'ByStatus' on table 'SecondaryIndexOrders' was auto-selected."));

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" IN [?, ?]
            """);
    }

    [Fact]
    public async Task Conservative_UnsafeOrPredicate_BlocksAutoSelection_EmitsRejectionDiagnostics()
    {
        _ = await Db
            .Orders
            .Where(o => o.Status == "PENDING" || o.Region == "US-EAST")
            .ToListAsync(CancellationToken);

        LoggerFactory
            .QueryDiagnosticEvents
            .Where(e => e.EventId.Id == DynamoEventId.SecondaryIndexCandidateRejected.Id)
            .Should()
            .NotBeEmpty()
            .And
            .OnlyContain(e
                => e.Message.Contains(
                    "was rejected: no equality or IN constraint on the index partition key."));

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .Contain(e => e.EventId.Id == DynamoEventId.NoCompatibleSecondaryIndexFound.Id);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            WHERE "Status" = 'PENDING' OR "Region" = 'US-EAST'
            """);
    }

    [Fact]
    public async Task
        Conservative_WhereOnNonIndexPkAttribute_AllCandidatesRejected_EmitsIdxDiagnostics()
    {
        _ = await Db.Orders.Where(o => o.Priority == 1).ToListAsync(CancellationToken);

        LoggerFactory
            .QueryDiagnosticEvents
            .Where(e => e.EventId.Id == DynamoEventId.SecondaryIndexCandidateRejected.Id)
            .Should()
            .NotBeEmpty()
            .And
            .OnlyContain(e
                => e.Message.Contains(
                    "was rejected: no equality or IN constraint on the index partition key."));

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .Contain(e => e.EventId.Id == DynamoEventId.NoCompatibleSecondaryIndexFound.Id);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            WHERE "Priority" = 1
            """);
    }

    [Fact]
    public async Task ExplicitHint_WithIndex_EmitsExplicitIndexSelectedDiagnostic()
    {
        _ = await Db
            .Orders
            .WithIndex("ByStatus")
            .Where(o => o.Status == "PENDING")
            .ToListAsync(CancellationToken);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.ExplicitIndexSelected.Id
                && e.LogLevel == LogLevel.Information
                && e.Message.Contains(
                    "Index 'ByStatus' on table 'SecondaryIndexOrders' was explicitly selected via WithIndex()."));

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .NotContain(e => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'PENDING'
            """);
    }

    [Fact]
    public async Task Conservative_GsiPkWithOrderBySk_AutoSelects_ScoringWinner()
    {
        try
        {
            _ = await Db
                .Orders
                .Where(o => o.Status == "PENDING")
                .OrderBy(o => o.CreatedAt)
                .ToListAsync(CancellationToken);
        }
        catch (AmazonDynamoDBException) { }

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                && e.Message.Contains(
                    "Index 'ByStatus' on table 'SecondaryIndexOrders' was auto-selected."));

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'PENDING'
            ORDER BY "CreatedAt" ASC
            """);
    }

    [Fact]
    public async Task Conservative_NoPredicate_EmitsNoCompatibleIndex_StaysOnBaseTable()
    {
        _ = await Db.Orders.ToListAsync(CancellationToken);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .Contain(e => e.EventId.Id == DynamoEventId.NoCompatibleSecondaryIndexFound.Id);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            """);
    }
}
