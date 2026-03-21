using System.Linq.Expressions;
using EntityFrameworkCore.DynamoDb.Diagnostics.Internal;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// DynamoDB provider post-processor that finalises the query expression and performs index
/// selection before SQL generation.
/// </summary>
/// <remarks>
/// <para>
/// Follows the same pattern as <c>CosmosQueryTranslationPostprocessor</c>:
/// <list type="number">
///   <item><description>Calls <c>base.Process</c> to apply standard EF post-processing.</description></item>
///   <item><description>Finalises the <c>SelectExpression</c> (discriminator predicate and projection)
///     so the analyzer can inspect the complete predicate tree and projection shape.</description></item>
///   <item><description>Pre-fetches runtime index descriptors from model annotations (once per compile).</description></item>
///   <item><description>Delegates to <c>IDynamoIndexSelectionAnalyzer</c> to choose an index.</description></item>
///   <item><description>Applies the chosen index name to the <c>SelectExpression</c> so the SQL generator
///     emits <c>FROM "Table"."Index"</c> or <c>FROM "Table"</c> accordingly.</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class DynamoQueryTranslationPostprocessor(
    QueryTranslationPostprocessorDependencies dependencies,
    DynamoQueryCompilationContext dynamoQueryCompilationContext,
    IDynamoIndexSelectionAnalyzer indexSelectionAnalyzer) : QueryTranslationPostprocessor(
    dependencies,
    dynamoQueryCompilationContext)
{
    /// <summary>
    /// Applies standard post-processing, finalises the <c>SelectExpression</c>, runs
    /// index-selection analysis, and applies the chosen index name to the query model.
    /// </summary>
    /// <returns>
    /// The post-processed expression tree with <c>SelectExpression.IndexName</c> set to
    /// the analyzer's chosen index, or <c>null</c> for base-table queries.
    /// </returns>
    public override Expression Process(Expression query)
    {
        query = base.Process(query);

        if (query is not ShapedQueryExpression
            {
                QueryExpression: SelectExpression selectExpression,
            })
            return query;

        // Finalise deferred discriminator predicate and projection so the analyzer can inspect
        // the complete WHERE clause and projection shape. These must run before analysis because:
        //   - The discriminator predicate affects which attributes are filtered on.
        //   - The projection affects which attributes must be available on the chosen index.
        selectExpression.ApplyDeferredDiscriminatorPredicate();
        selectExpression.ApplyProjection();

        // Pre-fetch candidates from runtime model annotations once per query; the analyzer must
        // not re-read annotations or rebuild metadata during property-level inspection.
        var candidates = ResolveCandidateDescriptors(
            dynamoQueryCompilationContext.Model.GetDynamoRuntimeTableModel(),
            selectExpression.TableName,
            selectExpression.QueryEntityTypeName);

        // Run structural constraint extraction when runtime candidates are available.
        // The visitor requires candidates to correctly classify safe vs unsafe OR expressions
        // (a filter-only OR on non-PK attributes does not set HasUnsafeOr).
        var queryConstraints = candidates.Count > 0
            ? new DynamoConstraintExtractionVisitor(candidates).Extract(selectExpression)
            : null;

        var mode =
            dynamoQueryCompilationContext.ContextOptions.FindExtension<DynamoDbOptionsExtension>()
                ?.AutomaticIndexSelectionMode
            ?? DynamoAutomaticIndexSelectionMode.Off;

        var analysisCtx = new DynamoIndexAnalysisContext
        {
            SelectExpression = selectExpression,
            ExplicitIndexHint = dynamoQueryCompilationContext.ExplicitIndexName,
            CandidateDescriptors = candidates,
            QueryEntityTypeName = selectExpression.QueryEntityTypeName,
            QueryConstraints = queryConstraints,
            AutomaticIndexSelectionMode = mode,
            IndexSelectionDisabled = dynamoQueryCompilationContext.IndexSelectionDisabled,
        };

        // IDynamoIndexSelectionAnalyzer is a DI singleton injected via the factory, so callers can
        // replace it with ReplaceService<IDynamoIndexSelectionAnalyzer, TCustom>() for steps 7–8
        // auto-selection or for test-time substitution.
        var decision = indexSelectionAnalyzer.Analyze(analysisCtx);

        if (decision.SelectedIndexName is { } chosen)
            selectExpression.ApplyIndexName(chosen);

        selectExpression.ApplyEffectivePartitionKeyPropertyNames(
            ResolveEffectivePartitionKeyPropertyNames(candidates, decision.SelectedIndexName));

        EmitIndexSelectionDiagnostics(decision.Diagnostics, dynamoQueryCompilationContext.Logger);

        // Validate ORDER BY constraints after index selection so the error references the
        // finalized query source (base table or chosen index).
        var effectiveSortKeyAttr =
            ResolveEffectiveSortKeyAttributeName(candidates, decision.SelectedIndexName);
        ValidateOrderByConstraints(selectExpression, queryConstraints, effectiveSortKeyAttr);

        return query;
    }

    /// <summary>
    /// Resolves the pre-fetched list of query source candidates for the given table and entity
    /// type from the <c>DynamoRuntimeTableModel</c> runtime annotation.
    /// </summary>
    /// The runtime model, or <c>null</c> when the model has not yet been initialised (design-time).
    /// The CLR name of the entity type being queried, used to scope candidates for shared-table
    /// models. When <c>null</c> (non-entity projection queries), all sources for the table are
    /// returned deduplicated by index name.
    /// <returns>
    /// The scoped list of <c>DynamoIndexDescriptor</c> candidates (base table first, then
    /// secondary indexes), or an empty list when the runtime model is unavailable or the table
    /// is not found.
    /// </returns>
    private static IReadOnlyList<DynamoIndexDescriptor> ResolveCandidateDescriptors(
        DynamoRuntimeTableModel? runtimeModel,
        string tableGroupName,
        string? queryEntityTypeName)
    {
        if (runtimeModel is null)
            return [];

        if (!runtimeModel.Tables.TryGetValue(tableGroupName, out var tableDescriptor))
            return [];

        // When the query entity type is known, scope to that type so that in a shared-table model
        // an index configured only on one entity type does not appear as a candidate for a query
        // against a different entity type sharing the same physical table.
        if (queryEntityTypeName is not null)
        {
            if (tableDescriptor.SourcesByQueryEntityTypeName.TryGetValue(
                queryEntityTypeName,
                out var scopedSources))
                return scopedSources;

            // Keep typed queries strict. Falling back to the union of all shared-table sources can
            // incorrectly expose indexes from unrelated entity types, which allows explicit hints
            // or auto-selection to choose an incomplete source for the queried entity set.
            return [];
        }

        // Non-entity projection queries have no entity type name; fall back to a deduplicated
        // union of all sources so explicit-hint validation can still succeed.
        return tableDescriptor
            .SourcesByQueryEntityTypeName
            .Values
            .SelectMany(v => v)
            .DistinctBy(d => d.IndexName)
            .ToList();
    }

    /// <summary>Resolves effective partition-key property names for the finalized query source.</summary>
    /// <returns>
    ///     A set containing the partition key for the finalized query source (base table when no
    ///     index is selected, or the selected index partition key when an index is selected).
    /// </returns>
    private static IReadOnlySet<string> ResolveEffectivePartitionKeyPropertyNames(
        IReadOnlyList<DynamoIndexDescriptor> candidates,
        string? selectedIndexName)
    {
        var propertyNames = new HashSet<string>(StringComparer.Ordinal);

        if (selectedIndexName is null)
        {
            if (candidates.FirstOrDefault(d => d.IndexName is null) is { } tableDescriptor)
                propertyNames.Add(tableDescriptor.PartitionKeyProperty.GetAttributeName());

            return propertyNames;
        }

        if (candidates.FirstOrDefault(d => d.IndexName == selectedIndexName) is { } indexDescriptor)
            propertyNames.Add(indexDescriptor.PartitionKeyProperty.GetAttributeName());

        return propertyNames;
    }

    /// <summary>
    ///     Resolves the sort-key attribute name for the finalized query source, or <c>null</c> when
    ///     the source has no sort key.
    /// </summary>
    /// <returns>
    ///     The DynamoDB attribute name of the sort key for the base table (when no index is selected)
    ///     or the selected index, or <c>null</c> when the source has no sort key.
    /// </returns>
    private static string? ResolveEffectiveSortKeyAttributeName(
        IReadOnlyList<DynamoIndexDescriptor> candidates,
        string? selectedIndexName)
    {
        // Find the descriptor for the finalized query source (base table when selectedIndexName is
        // null).
        var descriptor = selectedIndexName is null
            ? candidates.FirstOrDefault(d => d.IndexName is null)
            : candidates.FirstOrDefault(d => d.IndexName == selectedIndexName);

        return descriptor?.SortKeyProperty?.GetAttributeName();
    }

    /// <summary>
    ///     Validates ORDER BY constraints after index selection, throwing before SQL generation when
    ///     the query violates DynamoDB's ordering requirements.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         DynamoDB PartiQL imposes the following requirements on ORDER BY:
    ///         <list type="number">
    ///             <item>
    ///                 Each ordering column must be a key attribute (partition key or sort key) of the
    ///                 active query source. Non-key attributes are rejected regardless of PK constraints.
    ///             </item>
    ///             <item>
    ///                 For multi-partition queries (WHERE PK IN (...)), the partition key must appear
    ///                 first in the ORDER BY chain so DynamoDB can apply cross-partition ordering.
    ///             </item>
    ///             <item>
    ///                 The WHERE clause must contain an equality (<c>=</c>) or IN constraint on the
    ///                 partition key of the active query source.
    ///             </item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         Validation is skipped at design-time when <paramref name="queryConstraints" /> is
    ///         <c>null</c> (runtime model not yet built) to avoid false negatives during model building
    ///         and tooling.
    ///     </para>
    /// </remarks>
    private static void ValidateOrderByConstraints(
        SelectExpression selectExpression,
        DynamoQueryConstraints? queryConstraints,
        string? effectiveSortKeyAttributeName)
    {
        // No orderings — nothing to validate.
        if (selectExpression.Orderings.Count == 0)
            return;

        // Design-time: runtime model unavailable, skip to avoid false negatives.
        if (queryConstraints is null)
            return;

        var pkNames = selectExpression.EffectivePartitionKeyPropertyNames;

        // No descriptor resolved (edge case: model partially initialised) — skip silently.
        if (pkNames.Count == 0)
            return;

        // ── 1. Non-key attribute check ────────────────────────────────────────
        // Both PK and SK are valid ordering attributes. Non-key attributes are always rejected
        // because DynamoDB does not support arbitrary attribute ordering.
        var validKeyAttrs = new HashSet<string>(pkNames, StringComparer.Ordinal);
        if (effectiveSortKeyAttributeName is not null)
            validKeyAttrs.Add(effectiveSortKeyAttributeName);

        foreach (var ordering in selectExpression.Orderings)
        {
            if (ordering.Expression is not SqlPropertyExpression prop
                || !validKeyAttrs.Contains(prop.PropertyName))
            {
                var propName = ordering.Expression is SqlPropertyExpression p
                    ? p.PropertyName
                    : "?";
                var pkAttr = pkNames.First();
                var keyDesc = effectiveSortKeyAttributeName is not null
                    ? $"partition key '{pkAttr}' or sort key '{effectiveSortKeyAttributeName}'"
                    : $"partition key '{pkAttr}'";
                throw new InvalidOperationException(
                    $"ORDER BY can only reference key attributes ({keyDesc}) on table "
                    + $"'{selectExpression.TableName}'. '{propName}' is not a key. DynamoDB does "
                    + "not support ordering by arbitrary attributes.");
            }
        }

        // ── 2. Partition-key constraint check ─────────────────────────────────
        // DynamoDB requires the partition key to be equality-constrained when ORDER BY is used.
        foreach (var pkAttr in pkNames)
            if (queryConstraints.EqualityConstraints.ContainsKey(pkAttr))
                return;

        throw new InvalidOperationException(
            $"ORDER BY requires an equality constraint on the partition key in the WHERE "
            + $"clause for table '{selectExpression.TableName}'. DynamoDB only supports ordering "
            + "within a single partition. Add a WHERE predicate on the partition key "
            + "(e.g., .Where(e => e.PartitionKey == value)).");
    }

    /// <summary>Emits structured EF query diagnostics for index-selection analysis results.</summary>
    private static void EmitIndexSelectionDiagnostics(
        IReadOnlyList<DynamoQueryDiagnostic> diagnostics,
        IDiagnosticsLogger<DbLoggerCategory.Query> queryLogger)
    {
        foreach (var diagnostic in diagnostics)
            queryLogger.IndexSelectionDiagnostic(diagnostic);
    }
}
