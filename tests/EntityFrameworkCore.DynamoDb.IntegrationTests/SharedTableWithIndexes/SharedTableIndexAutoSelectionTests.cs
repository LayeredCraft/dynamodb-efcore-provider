using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTableWithIndexes;

public class SharedTableIndexAutoSelectionTests(DynamoContainerFixture fixture)
    : SharedTableWithIndexesTestFixture(fixture)
{
    [Fact]
    public async Task Conservative_DerivedTypeQuery_AutoSelects_ByPriorityGsi()
    {
        var results =
            await Db.PriorityWorkOrders.Where(o => o.Priority == 3).ToListAsync(CancellationToken);

        results
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeEquivalentTo(
                new PriorityWorkOrderEntity
                {
                    Pk = "WO#ALPHA", Sk = "WO#001", Status = "OPEN", Priority = 3,
                });

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                && e.LogLevel == LogLevel.Information
                && e.Message.Contains(
                    "Index 'ByPriority' on table 'work-orders-indexed-table' was auto-selected."));

        AssertSql(
            """
            SELECT "pk", "sk", "$type", "status", "priority"
            FROM "work-orders-indexed-table"."ByPriority"
            WHERE "priority" = 3 AND "$type" = 'PriorityWorkOrderEntity'
            """);
    }

    [Fact]
    public async Task Conservative_BaseTypeQuery_OrDiscriminatorIsSafe_AutoSelects_ByStatusLsi()
    {
        var results = await Db
            .WorkOrders
            .Where(o => o.Pk == "WO#ALPHA")
            .ToListAsync(CancellationToken);

        results
            .Should()
            .HaveCount(3)
            .And
            .Contain(o => o.Sk == "WO#001")
            .And
            .Contain(o => o.Sk == "WO#002")
            .And
            .Contain(o => o.Sk == "WO#003");

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                && e.Message.Contains(
                    "Index 'ByStatus' on table 'work-orders-indexed-table' was auto-selected."));

        AssertSql(
            """
            SELECT "pk", "sk", "$type", "status", "priority"
            FROM "work-orders-indexed-table"."ByStatus"
            WHERE "pk" = 'WO#ALPHA' AND ("$type" = 'ArchivedWorkOrderEntity' OR "$type" = 'PriorityWorkOrderEntity')
            """);
    }

    [Fact]
    public async Task Conservative_BaseTypeQuery_NoIndexPkConstraint_FallsBackToBaseTable()
    {
        _ = await Db.WorkOrders.Where(o => o.Status == "OPEN").ToListAsync(CancellationToken);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .Contain(e => e.EventId.Id == DynamoEventId.NoCompatibleSecondaryIndexFound.Id);

        AssertSql(
            """
            SELECT "pk", "sk", "$type", "status", "priority"
            FROM "work-orders-indexed-table"
            WHERE "status" = 'OPEN' AND ("$type" = 'ArchivedWorkOrderEntity' OR "$type" = 'PriorityWorkOrderEntity')
            """);
    }

    [Fact]
    public async Task Conservative_ArchivedWorkOrderQuery_ByPriorityGsiIsNotACandidate()
    {
        _ = await Db
            .ArchivedWorkOrders
            .Where(o => o.Pk == "WO#ALPHA")
            .ToListAsync(CancellationToken);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .NotContain(e => e.Message.Contains("ByPriority"));
    }

    [Fact]
    public async Task ExplicitHint_ByStatus_OnWorkOrders_EmitsIdx004()
    {
        _ = await Db
            .WorkOrders
            .WithIndex("ByStatus")
            .Where(o => o.Pk == "WO#ALPHA")
            .ToListAsync(CancellationToken);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.ExplicitIndexSelected.Id
                && e.LogLevel == LogLevel.Information
                && e.Message.Contains(
                    "Index 'ByStatus' on table 'work-orders-indexed-table' was explicitly selected via WithIndex()."));

        AssertSql(
            """
            SELECT "pk", "sk", "$type", "status", "priority"
            FROM "work-orders-indexed-table"."ByStatus"
            WHERE "pk" = 'WO#ALPHA' AND ("$type" = 'ArchivedWorkOrderEntity' OR "$type" = 'PriorityWorkOrderEntity')
            """);
    }

    [Fact]
    public async Task ExplicitHint_ByPriority_OnPriorityWorkOrders_EmitsIdx004()
    {
        var results = await Db
            .PriorityWorkOrders
            .WithIndex("ByPriority")
            .Where(o => o.Priority == 5)
            .ToListAsync(CancellationToken);

        results
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeEquivalentTo(
                new PriorityWorkOrderEntity
                {
                    Pk = "WO#BETA", Sk = "WO#001", Status = "OPEN", Priority = 5,
                });

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.ExplicitIndexSelected.Id
                && e.Message.Contains(
                    "Index 'ByPriority' on table 'work-orders-indexed-table' was explicitly selected via WithIndex()."));

        AssertSql(
            """
            SELECT "pk", "sk", "$type", "status", "priority"
            FROM "work-orders-indexed-table"."ByPriority"
            WHERE "priority" = 5 AND "$type" = 'PriorityWorkOrderEntity'
            """);
    }

    [Fact]
    public async Task ExplicitHint_SiblingEntityIndex_OnArchivedWorkOrders_Throws()
    {
        var act = async ()
            => await Db.ArchivedWorkOrders.WithIndex("ByPriority").ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ByPriority*");
    }

    [Fact]
    public async Task SuggestOnly_DerivedTypeQuery_EmitsDiagnosticButStaysOnBaseTable()
    {
        await using var suggestDb = CreateDbContext(DynamoAutomaticIndexSelectionMode.SuggestOnly);

        _ = await suggestDb
            .PriorityWorkOrders
            .Where(o => o.Priority == 3)
            .ToListAsync(CancellationToken);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                && e.Message.Contains(
                    "Index 'ByPriority' on table 'work-orders-indexed-table' would be selected in Conservative mode."));

        AssertSql(
            """
            SELECT "pk", "sk", "$type", "status", "priority"
            FROM "work-orders-indexed-table"
            WHERE "priority" = 3 AND "$type" = 'PriorityWorkOrderEntity'
            """);
    }
}
