using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// <see cref="IDynamoIndexSelectionAnalyzer"/> implementation that handles explicit
/// <c>.WithIndex()</c> hints and performs conservative automatic index selection based on
/// structural key-condition constraints extracted from the query predicate.
/// </summary>
/// <remarks>
/// <para>
/// An index is usable as a key-condition query source only when the WHERE clause contains an
/// equality (<c>=</c>) or IN constraint on the index's partition key. Sort-key conditions and
/// ordering alignment improve the score but do not gate eligibility. Only indexes with
/// <see cref="DynamoSecondaryIndexProjectionType.All"/> projection are considered as a
/// conservative guardrail until partial-projection coverage is implemented.
/// </para>
/// <para>
/// Diagnostic codes:
/// <list type="bullet">
///   <item><c>DYNAMO_IDX001</c> — no candidate satisfies the predicate (Warning)</item>
///   <item><c>DYNAMO_IDX002</c> — multiple candidates tie (Warning)</item>
///   <item><c>DYNAMO_IDX003</c> — a single candidate was selected or would be selected (Information)</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class DynamoAutoIndexSelectionAnalyzer : IDynamoIndexSelectionAnalyzer
{
    /// <summary>
    /// Validates an explicit hint when present, or evaluates candidate indexes against query
    /// constraints for automatic selection according to the configured mode.
    /// </summary>
    /// <param name="context">
    /// Compile-time snapshot including the explicit hint, pre-fetched runtime descriptors,
    /// extracted query constraints, and the configured automatic selection mode.
    /// </param>
    /// <returns>
    /// A <see cref="DynamoIndexSelectionDecision"/> naming the chosen index (or <c>null</c> for
    /// the base table) and any diagnostic observations produced during analysis.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an explicit hint names an index that is not registered for the queried table
    /// and entity type.
    /// </exception>
    public DynamoIndexSelectionDecision Analyze(DynamoIndexAnalysisContext context)
    {
        // ── 1. Explicit hint path ────────────────────────────────────────────
        if (context.ExplicitIndexHint is { } indexName)
            return HandleExplicitHint(context, indexName);

        // ── 2. Short-circuit: Off mode or no runtime constraints available ──
        if (context.QueryConstraints is null
            || context.AutomaticIndexSelectionMode == DynamoAutomaticIndexSelectionMode.Off)
            return new DynamoIndexSelectionDecision(null, DynamoIndexSelectionReason.NoSelection, []);

        // ── 3. Evaluate each secondary-index descriptor ──────────────────────
        var constraints = context.QueryConstraints;

        // Collect only indexes (not the base table descriptor) that pass all gates.
        // Base-table queries are the default — skipping the base-table descriptor here means
        // the auto-selection only considers switching the source to a secondary index.
        var usableCandidates = new List<(DynamoIndexDescriptor Descriptor, int Score)>();

        foreach (var descriptor in context.CandidateDescriptors)
        {
            // Only secondary indexes are auto-selected; the base table is the default path.
            if (descriptor.IndexName is null)
                continue;

            if (!PassesGates(descriptor, constraints))
                continue;

            var score = ComputeScore(descriptor, constraints);
            usableCandidates.Add((descriptor, score));
        }

        // ── 4. No usable candidate ───────────────────────────────────────────
        if (usableCandidates.Count == 0)
        {
            var diagnostics = new List<DynamoQueryDiagnostic>
            {
                new(DynamoQueryDiagnosticLevel.Warning,
                    "DYNAMO_IDX001",
                    $"No secondary index on table '{context.SelectExpression.TableName}' satisfies "
                    + "the predicate. The query will use the base table. "
                    + "Ensure the WHERE clause includes an equality constraint on the index partition key."),
            };
            return new DynamoIndexSelectionDecision(null, DynamoIndexSelectionReason.NoSelection, diagnostics);
        }

        // ── 5. Tie-break: highest score wins ─────────────────────────────────
        var topScore = usableCandidates.Max(c => c.Score);
        var winners = usableCandidates.Where(c => c.Score == topScore).ToList();

        if (winners.Count > 1)
        {
            var tiedNames = string.Join(", ", winners.Select(w => $"'{w.Descriptor.IndexName}'"));
            var diagnostics = new List<DynamoQueryDiagnostic>
            {
                new(DynamoQueryDiagnosticLevel.Warning,
                    "DYNAMO_IDX002",
                    $"Multiple secondary indexes on table '{context.SelectExpression.TableName}' "
                    + $"are equally suitable ({tiedNames}). Use .WithIndex() to specify the index explicitly."),
            };
            return new DynamoIndexSelectionDecision(null, DynamoIndexSelectionReason.NoSelection, diagnostics);
        }

        // ── 6. Single winner ────────────────────────────────────────────────
        var winner = winners[0].Descriptor;
        var mode = context.AutomaticIndexSelectionMode;

        var selectedDiagnostics = new List<DynamoQueryDiagnostic>
        {
            new(DynamoQueryDiagnosticLevel.Information,
                "DYNAMO_IDX003",
                mode == DynamoAutomaticIndexSelectionMode.Conservative
                    ? $"Index '{winner.IndexName}' on table '{context.SelectExpression.TableName}' was auto-selected."
                    : $"Index '{winner.IndexName}' on table '{context.SelectExpression.TableName}' would be selected in Conservative mode."),
        };

        if (mode == DynamoAutomaticIndexSelectionMode.Conservative)
            return new DynamoIndexSelectionDecision(
                winner.IndexName,
                DynamoIndexSelectionReason.AutoSelected,
                selectedDiagnostics);

        // SuggestOnly: emit the info diagnostic but do not change the query source.
        return new DynamoIndexSelectionDecision(null, DynamoIndexSelectionReason.NoSelection, selectedDiagnostics);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates and returns an explicit <c>.WithIndex()</c> hint, throwing when the named index
    /// is not registered for the queried table and entity type.
    /// </summary>
    private static DynamoIndexSelectionDecision HandleExplicitHint(
        DynamoIndexAnalysisContext context,
        string indexName)
    {
        // When no candidates are available (design-time; runtime model not yet built), skip
        // validation to avoid false negatives during model building and tooling scenarios.
        if (context.CandidateDescriptors.Count == 0)
            return new DynamoIndexSelectionDecision(indexName, DynamoIndexSelectionReason.ExplicitHint, []);

        var indexExists = context.CandidateDescriptors.Any(d => d.IndexName == indexName);

        if (!indexExists)
            throw new InvalidOperationException(
                $"Index '{indexName}' is not configured on table '{context.SelectExpression.TableName}'. "
                + "Use HasGlobalSecondaryIndex or HasLocalSecondaryIndex to register the "
                + "index before using WithIndex.");

        return new DynamoIndexSelectionDecision(indexName, DynamoIndexSelectionReason.ExplicitHint, []);
    }

    /// <summary>
    /// Returns <c>true</c> when the descriptor passes all gates required for it to be a usable
    /// key-condition query source:
    /// <list type="number">
    ///   <item>Gate 1 — PK covered: the constraints include an equality or IN on the index PK.</item>
    ///   <item>Gate 2 — Safe OR: the predicate has no unsafe OR that would corrupt result correctness.</item>
    ///   <item>Gate 3 — Projection safety: the index projects ALL attributes.</item>
    /// </list>
    /// </summary>
    private static bool PassesGates(DynamoIndexDescriptor descriptor, DynamoQueryConstraints constraints)
    {
        var pkAttr = descriptor.PartitionKeyProperty.GetAttributeName();

        // Gate 1: index partition key must be covered by an equality or IN constraint.
        var pkCovered = constraints.EqualityConstraints.ContainsKey(pkAttr)
            || constraints.InConstraints.ContainsKey(pkAttr);
        if (!pkCovered)
            return false;

        // Gate 2: any unsafe OR in the predicate means the extracted constraints do not
        // fully represent the filter, so auto-selection could return incorrect result sets.
        if (constraints.HasUnsafeOr)
            return false;

        // Gate 3: only project-ALL indexes are safe to auto-select until partial-projection
        // coverage (KEYS_ONLY / INCLUDE) is fully implemented.
        // TODO(partial-projection): lift this gate once attribute coverage analysis is added.
        if (descriptor.ProjectionType != DynamoSecondaryIndexProjectionType.All)
            return false;

        return true;
    }

    /// <summary>
    /// Computes a scoring bonus for a descriptor that already passed all gates.
    /// Higher score means the index better matches the query shape.
    /// </summary>
    /// <returns>
    /// A non-negative integer bonus: 0 for a bare PK match, up to 2 when the sort key is both
    /// constrained and aligned with an explicit query ordering.
    /// </returns>
    private static int ComputeScore(DynamoIndexDescriptor descriptor, DynamoQueryConstraints constraints)
    {
        var score = 0;
        var skAttr = descriptor.SortKeyProperty?.GetAttributeName();

        if (skAttr is null)
            return score;

        // +1 if the sort key has a range/equality condition — it becomes a key-condition query
        // rather than a filter, which is more efficient.
        if (constraints.SkKeyConditions.ContainsKey(skAttr))
            score++;

        // +1 if the query ordering aligns with this sort key. Absence of ORDER BY should not
        // prefer a sparse PK+SK index over a partition-only index because ordering is irrelevant
        // to result correctness in that shape.
        if (constraints.OrderingPropertyNames.Contains(skAttr))
            score++;

        return score;
    }
}
