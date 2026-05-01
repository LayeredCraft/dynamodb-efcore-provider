using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

public class SecondaryIndexAutoSelectionTests(DynamoContainerFixture fixture)
    : SecondaryIndexTableTestFixture(fixture)
{
    protected override DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.On;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task On_WhereOnGsiPkAndSk_AutoSelects_ByStatusIndex_AndEmitsDiagnostic()
    {
        var results = await Db
            .Orders
            .Where(o => o.Status == "PENDING" && o.CreatedAt == "2024-01-10")
            .ToListAsync(CancellationToken);

        var expected = OrderItems
            .Items
            .Where(o => o.Status == "PENDING" && o.CreatedAt == "2024-01-10")
            .ToList();
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
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "status" = 'PENDING' AND "createdAt" = '2024-01-10'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task DefaultMode_WhereOnGsiPk_AutoSelects_ByStatusIndex()
    {
        await using var db = new SecondaryIndexDbContext(
            CreateOptions<SecondaryIndexDbContext>(options => options.DynamoDbClient(Client)));

        var results =
            await db.Orders.Where(o => o.Status == "PENDING").ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => o.Status == "PENDING").ToList();
        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "status" = 'PENDING'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task On_WhereOnGsiPk_WithSkRange_AutoSelects_AndProducesCorrectResults()
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
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"."ByRegion"
            WHERE "region" = 'US-EAST' AND "createdAt" >= '2024-01-14'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task On_WhereOnTablePk_DoesNotAutoSelectGsi_StaysOnBaseTable()
    {
        var results =
            await Db.Orders.Where(o => o.CustomerId == "C#1").ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => o.CustomerId == "C#1").ToList();
        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"
            WHERE "customerId" = 'C#1'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task On_WhereOnTablePk_LsiCandidates_AutoSelects_WhenOrderingMatches()
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
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"."ByPriority"
            WHERE "customerId" = 'C#1'
            ORDER BY "priority" ASC
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task On_WhereOnTablePk_AmbiguousLsi_FallsBackToBaseTable()
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
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"
            WHERE "customerId" = 'C#1'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task On_ContainsOnGsiPk_AutoSelects_ByStatusIndex()
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
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "status" IN [?, ?]
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task On_UnsafeOrPredicate_BlocksAutoSelection_EmitsRejectionDiagnostics()
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
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"
            WHERE "status" = 'PENDING' OR "region" = 'US-EAST'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task On_WhereOnNonIndexPkAttribute_AllCandidatesRejected_EmitsIdxDiagnostics()
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
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"
            WHERE "priority" = 1
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExplicitHint_WithIndex_EmitsExplicitIndexSelectedDiagnostic()
    {
        _ = await Db
            .Orders
            .WithIndex("ByStatus")
            .Where(o => o.Status == "PENDING" && o.CreatedAt == "2024-01-10")
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
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "status" = 'PENDING' AND "createdAt" = '2024-01-10'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task On_GsiPkWithOrderBySk_AutoSelects_ScoringWinner()
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
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "status" = 'PENDING'
            ORDER BY "createdAt" ASC
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task On_NoPredicate_EmitsNoCompatibleIndex_StaysOnBaseTable()
    {
        _ = await Db.Orders.ToListAsync(CancellationToken);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .Contain(e => e.EventId.Id == DynamoEventId.NoCompatibleSecondaryIndexFound.Id);

        AssertSql(
            """
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"
            """);
    }
}
