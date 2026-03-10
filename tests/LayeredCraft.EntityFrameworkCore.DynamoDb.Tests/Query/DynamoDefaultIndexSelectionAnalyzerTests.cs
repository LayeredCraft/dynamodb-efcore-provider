using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using NSubstitute;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Unit tests for <see cref="DynamoDefaultIndexSelectionAnalyzer"/>.</summary>
public class DynamoDefaultIndexSelectionAnalyzerTests
{
    private static readonly DynamoDefaultIndexSelectionAnalyzer Analyzer = new();

    // ── helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal <see cref="DynamoIndexDescriptor"/> using substituted EF metadata.
    /// Only <see cref="DynamoIndexDescriptor.IndexName"/> is inspected by the analyzer in step 6.
    /// </summary>
    private static DynamoIndexDescriptor MakeDescriptor(string? indexName)
    {
        var pkProperty = Substitute.For<IReadOnlyProperty>();
        return new DynamoIndexDescriptor(
            IndexName: indexName,
            Kind: indexName is null ? DynamoIndexSourceKind.Table : DynamoIndexSourceKind.GlobalSecondaryIndex,
            ModelIndex: null,
            PartitionKeyProperty: pkProperty,
            SortKeyProperty: null,
            ProjectionType: DynamoSecondaryIndexProjectionType.All);
    }

    private static DynamoIndexAnalysisContext BuildContext(
        string? explicitHint,
        IReadOnlyList<DynamoIndexDescriptor> candidates,
        string tableName = "Orders",
        string? queryEntityTypeName = "Order")
        => new()
        {
            SelectExpression    = new SelectExpression(tableName, queryEntityTypeName),
            ExplicitIndexHint   = explicitHint,
            CandidateDescriptors = candidates,
            QueryEntityTypeName = queryEntityTypeName,
        };

    // ── no hint ─────────────────────────────────────────────────────────────

    [Fact]
    public void NoHint_Returns_NoSelection_WithNullIndexName()
    {
        var ctx = BuildContext(null, [MakeDescriptor(null), MakeDescriptor("ByStatus")]);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().BeNull();
        decision.Reason.Should().Be(DynamoIndexSelectionReason.NoSelection);
        decision.Diagnostics.Should().BeEmpty();
    }

    // ── explicit hint ────────────────────────────────────────────────────────

    [Fact]
    public void ExplicitHint_KnownIndex_Returns_ExplicitHintDecision()
    {
        var ctx = BuildContext(
            "ByStatus",
            [MakeDescriptor(null), MakeDescriptor("ByStatus"), MakeDescriptor("ByRegion")]);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().Be("ByStatus");
        decision.Reason.Should().Be(DynamoIndexSelectionReason.ExplicitHint);
        decision.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ExplicitHint_UnknownIndex_ThrowsInvalidOperationException()
    {
        var ctx = BuildContext(
            "DoesNotExist",
            [MakeDescriptor(null), MakeDescriptor("ByStatus")]);

        var act = () => Analyzer.Analyze(ctx);

        act.Should()
           .Throw<InvalidOperationException>()
           .WithMessage("*DoesNotExist*");
    }

    [Fact]
    public void ExplicitHint_UnknownIndex_ErrorMessage_IncludesTableName()
    {
        var ctx = BuildContext(
            "Ghost",
            [MakeDescriptor(null)],
            tableName: "MyTable");

        var act = () => Analyzer.Analyze(ctx);

        act.Should()
           .Throw<InvalidOperationException>()
           .WithMessage("*MyTable*");
    }

    /// <summary>
    /// When candidates are empty the runtime model has not been initialised (design-time / tooling
    /// scenario). Validation must be skipped rather than throwing a false-negative error.
    /// </summary>
    [Fact]
    public void ExplicitHint_NoCandidates_SkipsValidationAndReturnsExplicitHint()
    {
        var ctx = BuildContext("ByStatus", []);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().Be("ByStatus");
        decision.Reason.Should().Be(DynamoIndexSelectionReason.ExplicitHint);
    }

    /// <summary>
    /// In a shared-table model, an index configured only on one entity type must not pass
    /// validation for a query that has candidates pre-scoped to a different entity type.
    /// (The scoping is performed by DynamoQueryTranslationPostprocessor when building candidates.)
    /// </summary>
    [Fact]
    public void ExplicitHint_SharedTable_WrongEntityType_CandidatesDoNotContainIndex_Throws()
    {
        // The postprocessor scopes candidates to the query entity type, so Invoice candidates
        // only contain the base-table descriptor — "ByStatus" is NOT present.
        var invoiceCandidates = new List<DynamoIndexDescriptor> { MakeDescriptor(null) };
        var ctx = BuildContext("ByStatus", invoiceCandidates, queryEntityTypeName: "Invoice");

        var act = () => Analyzer.Analyze(ctx);

        act.Should()
           .Throw<InvalidOperationException>()
           .WithMessage("*ByStatus*");
    }

    [Fact]
    public void ExplicitHint_MatchesBaseTableDescriptor_ShouldNotThrow()
    {
        // Passing the base-table's null IndexName as the hint is unusual, but the analyzer should
        // not throw — it searches by exact match and null != any secondary index name.
        var ctx = BuildContext(
            "ByStatus",
            [MakeDescriptor(null), MakeDescriptor("ByStatus")]);

        var act = () => Analyzer.Analyze(ctx);

        act.Should().NotThrow();
    }
}
