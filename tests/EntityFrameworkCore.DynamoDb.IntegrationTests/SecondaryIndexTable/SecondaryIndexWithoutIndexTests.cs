using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>
/// Integration tests for <c>.WithoutIndex()</c> — verifies that the method forces base-table
/// execution even when the query shape would otherwise trigger automatic index selection, and
/// that the <c>DYNAMO_IDX006</c> diagnostic is emitted when the suppression is applied.
/// </summary>
public class SecondaryIndexWithoutIndexTests(SecondaryIndexDynamoFixture fixture)
    : SecondaryIndexTestBase(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    /// Overrides the base options to enable <c>DynamoAutomaticIndexSelectionMode.Conservative</c>
    /// so that queries that don't call <c>.WithoutIndex()</c> would normally get an index auto-selected,
    /// making the suppression clearly observable.
    /// </remarks>
    protected override DbContextOptions<SecondaryIndexDbContext> CreateOptions(
        TestPartiQlLoggerFactory loggerFactory)
    {
        var builder =
            new DbContextOptionsBuilder<SecondaryIndexDbContext>(base.CreateOptions(loggerFactory));
        builder.UseDynamo(opt
            => opt.UseAutomaticIndexSelection(DynamoAutomaticIndexSelectionMode.Conservative));
        return builder.Options;
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task WithoutIndex_ConservativeMode_UsesBaseTable_AndEmitsDiagnosticIDX006()
    {
        // Without .WithoutIndex(), this query would auto-select the ByStatus GSI (Status equality
        // satisfies the PK gate). The suppression must force the FROM clause back to the base
        // table.
        var results =
            await Db
                .Orders
                .WithoutIndex()
                .Where(o => o.Status == "PENDING")
                .ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => o.Status == "PENDING").ToList();
        results.Should().BeEquivalentTo(expected);

        // IDX006 must be emitted to record that suppression was applied.
        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.ExplicitIndexSelectionDisabled.Id
                && e.LogLevel == LogLevel.Information
                && e.Message.Contains(".WithoutIndex()"));

        // The FROM clause must reference the base table, not any secondary index.
        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            WHERE "Status" = 'PENDING'
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task WithoutIndex_DoesNotEmitAutoSelectionDiagnostics()
    {
        // When .WithoutIndex() is present the analyzer short-circuits before evaluation, so
        // IDX001–IDX005 must not appear in the log for this query.
        await Db
            .Orders
            .WithoutIndex()
            .Where(o => o.Status == "PENDING")
            .ToListAsync(CancellationToken);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .NotContain(e
                => e.EventId.Id == DynamoEventId.NoCompatibleSecondaryIndexFound.Id
                || e.EventId.Id == DynamoEventId.MultipleCompatibleSecondaryIndexesFound.Id
                || e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                || e.EventId.Id == DynamoEventId.SecondaryIndexCandidateRejected.Id);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task WithoutIndex_WithExplicitHint_ThrowsInvalidOperationException()
    {
        // Combining .WithIndex() and .WithoutIndex() on the same query must throw at compile time.
        var act = async ()
            => await Db
                .Orders
                .WithIndex("ByStatus")
                .WithoutIndex()
                .Where(o => o.Status == "PENDING")
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*'.WithIndex()'*'.WithoutIndex()'*");
    }
}
