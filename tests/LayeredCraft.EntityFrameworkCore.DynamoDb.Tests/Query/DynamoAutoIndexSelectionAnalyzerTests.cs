using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using NSubstitute;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Unit tests for <see cref="DynamoAutoIndexSelectionAnalyzer"/>.</summary>
public class DynamoAutoIndexSelectionAnalyzerTests
{
    private static readonly DynamoAutoIndexSelectionAnalyzer Analyzer = new();

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal substituted <see cref="IReadOnlyProperty"/> whose
    /// <see cref="IReadOnlyProperty.Name"/> returns <paramref name="attrName"/>.
    /// <c>GetAttributeName()</c> falls back to <c>property.Name</c> when the annotation is null.
    /// </summary>
    private static IReadOnlyProperty MakeProp(string attrName)
    {
        var prop = Substitute.For<IReadOnlyProperty>();
        prop.Name.Returns(attrName);
        return prop;
    }

    /// <summary>
    /// Builds a minimal <see cref="DynamoIndexDescriptor"/> with the given partition key,
    /// optional sort key, index name, and projection type.
    /// </summary>
    private static DynamoIndexDescriptor MakeDescriptor(
        string pkAttr,
        string? skAttr = null,
        string? indexName = null,
        DynamoSecondaryIndexProjectionType projectionType = DynamoSecondaryIndexProjectionType.All)
        => new(
            IndexName: indexName,
            Kind: indexName is null ? DynamoIndexSourceKind.Table : DynamoIndexSourceKind.GlobalSecondaryIndex,
            ModelIndex: null,
            PartitionKeyProperty: MakeProp(pkAttr),
            SortKeyProperty: skAttr is null ? null : MakeProp(skAttr),
            ProjectionType: projectionType);

    private static SqlConstantExpression Const(object value)
        => new(value, value.GetType(), null);

    /// <summary>
    /// Builds a <see cref="DynamoQueryConstraints"/> directly for unit tests, bypassing the
    /// visitor pipeline.
    /// </summary>
    private static DynamoQueryConstraints MakeConstraints(
        string[]? equalityPks = null,
        string[]? inPks = null,
        string[]? skConditions = null,
        bool hasUnsafeOr = false,
        string[]? orderings = null)
        => new(
            EqualityConstraints: (equalityPks ?? [])
                .ToDictionary(k => k, _ => (SqlExpression)Const("v")),
            InConstraints: (inPks ?? [])
                .ToDictionary(k => k, _ => (IReadOnlyList<SqlExpression>)[Const("v")]),
            SkKeyConditions: (skConditions ?? [])
                .ToDictionary(k => k, _ => new SkConstraint(SkOperator.Equal, Const("v"))),
            HasUnsafeOr: hasUnsafeOr,
            OrderingPropertyNames: new HashSet<string>(orderings ?? []));

    private static DynamoIndexAnalysisContext BuildContext(
        DynamoAutomaticIndexSelectionMode mode,
        IReadOnlyList<DynamoIndexDescriptor> candidates,
        DynamoQueryConstraints? constraints,
        string? explicitHint = null,
        string tableName = "Orders",
        string? queryEntityTypeName = "Order")
        => new()
        {
            SelectExpression = new SelectExpression(tableName, queryEntityTypeName),
            ExplicitIndexHint = explicitHint,
            CandidateDescriptors = candidates,
            QueryEntityTypeName = queryEntityTypeName,
            QueryConstraints = constraints,
            AutomaticIndexSelectionMode = mode,
        };

    // ── absorbed: explicit hint tests ────────────────────────────────────────

    [Fact]
    public void ExplicitHint_KnownIndex_Returns_ExplicitHintDecision()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
            MakeDescriptor("Region", "CreatedAt", "ByRegion"),
        };
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Off, candidates, null, "ByStatus");

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().Be("ByStatus");
        decision.Reason.Should().Be(DynamoIndexSelectionReason.ExplicitHint);
        decision.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ExplicitHint_UnknownIndex_ThrowsInvalidOperationException()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
        };
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Off, candidates, null, "DoesNotExist");

        var act = () => Analyzer.Analyze(ctx);

        act.Should().Throw<InvalidOperationException>().WithMessage("*DoesNotExist*");
    }

    [Fact]
    public void ExplicitHint_UnknownIndex_ErrorMessage_IncludesTableName()
    {
        var candidates = new List<DynamoIndexDescriptor> { MakeDescriptor("PK", indexName: null) };
        var ctx = BuildContext(
            DynamoAutomaticIndexSelectionMode.Off,
            candidates,
            null,
            "Ghost",
            tableName: "MyTable");

        var act = () => Analyzer.Analyze(ctx);

        act.Should().Throw<InvalidOperationException>().WithMessage("*MyTable*");
    }

    [Fact]
    public void ExplicitHint_NoCandidates_SkipsValidationAndReturnsExplicitHint()
    {
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Off, [], null, "ByStatus");

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().Be("ByStatus");
        decision.Reason.Should().Be(DynamoIndexSelectionReason.ExplicitHint);
    }

    /// <summary>
    /// In a shared-table model the postprocessor scopes candidates to the query entity type,
    /// so an index for a different entity type does not appear in candidates and must be rejected.
    /// </summary>
    [Fact]
    public void ExplicitHint_SharedTable_WrongEntityType_CandidatesDoNotContainIndex_Throws()
    {
        var invoiceCandidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("InvoiceId", indexName: null),
        };
        var ctx = BuildContext(
            DynamoAutomaticIndexSelectionMode.Off,
            invoiceCandidates,
            null,
            "ByStatus",
            queryEntityTypeName: "Invoice");

        var act = () => Analyzer.Analyze(ctx);

        act.Should().Throw<InvalidOperationException>().WithMessage("*ByStatus*");
    }

    [Fact]
    public void ExplicitHint_MatchesBaseTableDescriptor_ShouldNotThrow()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
        };
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Off, candidates, null, "ByStatus");

        var act = () => Analyzer.Analyze(ctx);

        act.Should().NotThrow();
    }

    [Fact]
    public void ExplicitHint_KeysOnlyProjectionIndex_Returns_ExplicitHintDecision()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor(
                "Status",
                "CreatedAt",
                "ByStatus",
                DynamoSecondaryIndexProjectionType.KeysOnly),
        };
        var ctx = BuildContext(
            DynamoAutomaticIndexSelectionMode.Off,
            candidates,
            MakeConstraints(["Status"]),
            "ByStatus");

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().Be("ByStatus");
        decision.Reason.Should().Be(DynamoIndexSelectionReason.ExplicitHint);
        decision.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ExplicitHint_IncludeProjectionIndex_Returns_ExplicitHintDecision()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor(
                "Status",
                "CreatedAt",
                "ByStatus",
                DynamoSecondaryIndexProjectionType.Include),
        };
        var ctx = BuildContext(
            DynamoAutomaticIndexSelectionMode.Off,
            candidates,
            MakeConstraints(["Status"]),
            "ByStatus");

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().Be("ByStatus");
        decision.Reason.Should().Be(DynamoIndexSelectionReason.ExplicitHint);
        decision.Diagnostics.Should().BeEmpty();
    }

    // ── no hint, Off mode ────────────────────────────────────────────────────

    [Fact]
    public void NoHint_Returns_NoSelection_WithNullIndexName()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
        };
        var constraints = MakeConstraints(equalityPks: ["Status"]);
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Off, candidates, constraints);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().BeNull();
        decision.Reason.Should().Be(DynamoIndexSelectionReason.NoSelection);
        decision.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Off_Mode_PkPresent_Returns_NoSelection_WithoutDiagnostics()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
        };
        var constraints = MakeConstraints(equalityPks: ["Status"]);
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Off, candidates, constraints);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().BeNull();
        decision.Reason.Should().Be(DynamoIndexSelectionReason.NoSelection);
        decision.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Off_Mode_NullConstraints_Returns_NoSelection()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
        };
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Off, candidates, null);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().BeNull();
        decision.Reason.Should().Be(DynamoIndexSelectionReason.NoSelection);
        decision.Diagnostics.Should().BeEmpty();
    }

    // ── SuggestOnly mode ─────────────────────────────────────────────────────

    [Fact]
    public void SuggestOnly_SingleMatch_EmitsInfoDiagnostic_Returns_NoSelection()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
        };
        var constraints = MakeConstraints(equalityPks: ["Status"]);
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.SuggestOnly, candidates, constraints);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().BeNull();
        decision.Reason.Should().Be(DynamoIndexSelectionReason.NoSelection);
        decision.Diagnostics.Should().HaveCount(1);
        decision.Diagnostics[0].Code.Should().Be("DYNAMO_IDX003");
        decision.Diagnostics[0].Level.Should().Be(DynamoQueryDiagnosticLevel.Information);
        decision.Diagnostics[0].Message.Should().Contain("ByStatus");
        decision.Diagnostics[0].Message.Should().Contain("would be selected");
    }

    [Fact]
    public void SuggestOnly_NoMatch_EmitsWarningDiagnostic_Returns_NoSelection()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
        };
        // No equality on Status — no candidate passes Gate 1.
        var constraints = MakeConstraints(equalityPks: ["CustomerId"]);
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.SuggestOnly, candidates, constraints);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().BeNull();
        decision.Reason.Should().Be(DynamoIndexSelectionReason.NoSelection);
        decision.Diagnostics.Should().HaveCount(1);
        decision.Diagnostics[0].Code.Should().Be("DYNAMO_IDX001");
        decision.Diagnostics[0].Level.Should().Be(DynamoQueryDiagnosticLevel.Warning);
    }

    // ── Conservative mode ────────────────────────────────────────────────────

    [Fact]
    public void Conservative_SingleMatch_NoBonuses_Returns_AutoSelected()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", indexName: "ByStatus"),
        };
        var constraints = MakeConstraints(equalityPks: ["Status"]);
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Conservative, candidates, constraints);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().Be("ByStatus");
        decision.Reason.Should().Be(DynamoIndexSelectionReason.AutoSelected);
        decision.Diagnostics.Should().HaveCount(1);
        decision.Diagnostics[0].Code.Should().Be("DYNAMO_IDX003");
        decision.Diagnostics[0].Message.Should().Contain("auto-selected");
    }

    [Fact]
    public void Conservative_NoCandidateSatisfied_EmitsIdx001Warning_NoSelection()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
        };
        // CustomerId equality does not cover Status PK.
        var constraints = MakeConstraints(equalityPks: ["CustomerId"]);
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Conservative, candidates, constraints);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().BeNull();
        decision.Reason.Should().Be(DynamoIndexSelectionReason.NoSelection);
        decision.Diagnostics.Should().HaveCount(1);
        decision.Diagnostics[0].Code.Should().Be("DYNAMO_IDX001");
    }

    [Fact]
    public void Conservative_AmbiguousTie_EmitsIdx002Warning_NoSelection()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
            MakeDescriptor("Region", "CreatedAt", "ByRegion"),
        };
        // Both GSI PKs are present and neither gets a sort-key bonus.
        var constraints = MakeConstraints(equalityPks: ["Status", "Region"]);
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Conservative, candidates, constraints);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().BeNull();
        decision.Reason.Should().Be(DynamoIndexSelectionReason.NoSelection);
        decision.Diagnostics.Should().HaveCount(1);
        decision.Diagnostics[0].Code.Should().Be("DYNAMO_IDX002");
        decision.Diagnostics[0].Message.Should().Contain("ByStatus");
        decision.Diagnostics[0].Message.Should().Contain("ByRegion");
    }

    [Fact]
    public void Conservative_NoOrdering_DoesNotPreferSortKeyIndexOverPartitionOnlyIndex()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", indexName: "ByStatus"),
            MakeDescriptor("Status", "CreatedAt", "ByStatusCreatedAt"),
        };
        var constraints = MakeConstraints(["Status"]);
        var ctx = BuildContext(
            DynamoAutomaticIndexSelectionMode.Conservative,
            candidates,
            constraints);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().BeNull();
        decision.Reason.Should().Be(DynamoIndexSelectionReason.NoSelection);
        decision.Diagnostics.Should().HaveCount(1);
        decision.Diagnostics[0].Code.Should().Be("DYNAMO_IDX002");
        decision.Diagnostics[0].Message.Should().Contain("ByStatus");
        decision.Diagnostics[0].Message.Should().Contain("ByStatusCreatedAt");
    }

    [Fact]
    public void Conservative_SkBonusTiebreaks_ClearWinner_AutoSelected()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
            MakeDescriptor("Region", "CreatedAt", "ByRegion"),
        };
        // Both GSI PKs present, but only ByStatus has a SK condition.
        var constraints = MakeConstraints(
            equalityPks: ["Status", "Region"],
            skConditions: ["CreatedAt"]);
        // ByStatus SK = "CreatedAt" → score 1; ByRegion SK = "CreatedAt" → also score 1 ... tie?
        // Wait — both have the same SK attr "CreatedAt" and both would benefit. Let me reconsider.
        // Actually both descriptors have the same skAttr "CreatedAt", so both get +1.
        // We need a different setup for tie-breaking via SK.
        // Use ByStatus with SK "CreatedAt" (gets +1) and ByRegion with SK "Priority" (gets 0).
        var candidatesSkTiebreak = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
            MakeDescriptor("Region", "Priority", "ByRegion"),
        };
        var constraintsSkTiebreak = MakeConstraints(
            equalityPks: ["Status", "Region"],
            skConditions: ["CreatedAt"]);
        var ctx = BuildContext(
            DynamoAutomaticIndexSelectionMode.Conservative,
            candidatesSkTiebreak,
            constraintsSkTiebreak);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().Be("ByStatus");
        decision.Reason.Should().Be(DynamoIndexSelectionReason.AutoSelected);
    }

    [Fact]
    public void Conservative_OrderingBonusTiebreaks_ClearWinner_AutoSelected()
    {
        // Both candidates have PK covered and no SK condition, but ordering aligns with ByStatus.
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
            MakeDescriptor("Region", "Priority", "ByRegion"),
        };
        var constraints = MakeConstraints(
            equalityPks: ["Status", "Region"],
            orderings: ["CreatedAt"]);
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Conservative, candidates, constraints);

        var decision = Analyzer.Analyze(ctx);

        // ByStatus has SK "CreatedAt" which is in orderings → +1; ByRegion SK "Priority" is not → 0.
        decision.SelectedIndexName.Should().Be("ByStatus");
        decision.Reason.Should().Be(DynamoIndexSelectionReason.AutoSelected);
    }

    [Fact]
    public void Conservative_BothBonuses_BeatsSkOnly_AutoSelected()
    {
        // ByStatus gets +2 (SK condition + ordering); ByRegion gets +1 (SK condition only).
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
            MakeDescriptor("Region", "Priority", "ByRegion"),
        };
        var constraints = MakeConstraints(
            equalityPks: ["Status", "Region"],
            skConditions: ["CreatedAt", "Priority"],
            orderings: ["CreatedAt"]);
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Conservative, candidates, constraints);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().Be("ByStatus");
        decision.Reason.Should().Be(DynamoIndexSelectionReason.AutoSelected);
    }

    [Fact]
    public void Conservative_UnsafeOrBlocksAllCandidates_NoSelection()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
        };
        var constraints = MakeConstraints(equalityPks: ["Status"], hasUnsafeOr: true);
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Conservative, candidates, constraints);

        var decision = Analyzer.Analyze(ctx);

        // HasUnsafeOr blocks Gate 2 for all candidates, so IDX001 warning is emitted.
        decision.SelectedIndexName.Should().BeNull();
        decision.Diagnostics.Should().HaveCount(1);
        decision.Diagnostics[0].Code.Should().Be("DYNAMO_IDX001");
    }

    [Fact]
    public void Conservative_NonAllProjectionDescriptor_Excluded_NoSelection()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus", DynamoSecondaryIndexProjectionType.KeysOnly),
        };
        var constraints = MakeConstraints(equalityPks: ["Status"]);
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Conservative, candidates, constraints);

        var decision = Analyzer.Analyze(ctx);

        // Gate 3 excludes non-All projection descriptors.
        decision.SelectedIndexName.Should().BeNull();
        decision.Diagnostics.Should().HaveCount(1);
        decision.Diagnostics[0].Code.Should().Be("DYNAMO_IDX001");
    }

    [Fact]
    public void Conservative_InConstraint_SatisfiesPkGate_AutoSelected()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
        };
        // IN constraint on Status PK satisfies Gate 1.
        var constraints = MakeConstraints(inPks: ["Status"]);
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Conservative, candidates, constraints);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().Be("ByStatus");
        decision.Reason.Should().Be(DynamoIndexSelectionReason.AutoSelected);
    }

    [Fact]
    public void Conservative_NullQueryConstraints_Returns_NoSelection()
    {
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", indexName: null),
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
        };
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Conservative, candidates, null);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().BeNull();
        decision.Reason.Should().Be(DynamoIndexSelectionReason.NoSelection);
        decision.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Conservative_BaseTableDescriptor_Skipped_OnlyIndexesEvaluated()
    {
        // The base-table descriptor has the same PK attr as the query equality constraint.
        // It must be skipped — auto-selection should only consider secondary indexes.
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("Status", indexName: null),   // base table (IndexName == null)
            MakeDescriptor("Status", "CreatedAt", "ByStatus"),
        };
        var constraints = MakeConstraints(equalityPks: ["Status"]);
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Conservative, candidates, constraints);

        var decision = Analyzer.Analyze(ctx);

        // The base-table descriptor is skipped, ByStatus passes — auto-selected.
        decision.SelectedIndexName.Should().Be("ByStatus");
        decision.Reason.Should().Be(DynamoIndexSelectionReason.AutoSelected);
    }

    [Fact]
    public void Conservative_LsiCandidate_SatisfiedByTablePk_AutoSelected()
    {
        // LSI shares the base-table partition key (CustomerId). When the WHERE has CustomerId
        // equality, the LSI can be used as a key-condition query source.
        var candidates = new List<DynamoIndexDescriptor>
        {
            MakeDescriptor("CustomerId", "OrderId", indexName: null),
            new(
                IndexName: "ByPriority",
                Kind: DynamoIndexSourceKind.LocalSecondaryIndex,
                ModelIndex: null,
                PartitionKeyProperty: MakeProp("CustomerId"),
                SortKeyProperty: MakeProp("Priority"),
                ProjectionType: DynamoSecondaryIndexProjectionType.All),
        };
        var constraints = MakeConstraints(equalityPks: ["CustomerId"]);
        var ctx = BuildContext(DynamoAutomaticIndexSelectionMode.Conservative, candidates, constraints);

        var decision = Analyzer.Analyze(ctx);

        decision.SelectedIndexName.Should().Be("ByPriority");
        decision.Reason.Should().Be(DynamoIndexSelectionReason.AutoSelected);
    }
}
