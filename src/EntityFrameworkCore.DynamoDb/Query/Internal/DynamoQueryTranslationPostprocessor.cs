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
                QueryExpression: SelectExpression selectExpression
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

        var options =
            dynamoQueryCompilationContext.ContextOptions.FindExtension<DynamoDbOptionsExtension>();

        var mode = options?.AutomaticIndexSelectionMode ?? DynamoAutomaticIndexSelectionMode.On;

        var analysisCtx = new DynamoIndexAnalysisContext
        {
            SelectExpression = selectExpression,
            ExplicitIndexHint = dynamoQueryCompilationContext.ExplicitIndexName,
            CandidateDescriptors = candidates,
            QueryEntityTypeName = selectExpression.QueryEntityTypeName,
            QueryConstraints = queryConstraints,
            AutomaticIndexSelectionMode = mode,
            IndexSelectionDisabled = dynamoQueryCompilationContext.IndexSelectionDisabled
        };

        // IDynamoIndexSelectionAnalyzer is a DI singleton injected via the factory, so callers can
        // replace it with ReplaceService<IDynamoIndexSelectionAnalyzer, TCustom>() for
        // auto-selection or for test-time substitution.
        var decision = indexSelectionAnalyzer.Analyze(analysisCtx);
        var selectedIndexName = decision.SelectedIndexName;
        EmitIndexSelectionDiagnostics(decision.Diagnostics, dynamoQueryCompilationContext.Logger);

        if (selectedIndexName is { } chosen)
            selectExpression.ApplyIndexName(chosen);

        selectExpression.ApplyIndexSourceKind(
            ResolveIndexSourceKind(candidates, decision.SelectedIndexName));

        selectExpression.ApplyEffectivePartitionKeyPropertyNames(
            ResolveEffectivePartitionKeyPropertyNames(candidates, selectedIndexName));

        // Validate ORDER BY constraints after index selection so the error references the
        // finalized query source (base table or chosen index).
        var effectiveSortKeyAttr =
            ResolveEffectiveSortKeyAttributeName(candidates, selectedIndexName);
        var isBaseTableSource = selectedIndexName is null;

        selectExpression.ApplyScanQueryClassification(
            ClassifyScanQuery(
                selectExpression,
                queryConstraints,
                effectiveSortKeyAttr,
                isBaseTableSource));

        ValidateOrderByConstraints(selectExpression, queryConstraints, effectiveSortKeyAttr);

        // Validate First*/Single* safe paths after index selection so we use the effective key
        // attributes of the chosen source.
        if (selectExpression.IsFirstTerminal)
        {
            // Explicit Limit(n) + First* is ambiguous: Limit(n) may intentionally return
            // multiple items, while First* requires provider-managed Limit(1). Keep this
            // unconditional; AsUnsafeFilteredQuery() only relaxes non-key filter validation.
            if (selectExpression.HasUserLimit)
                throw new InvalidOperationException(
                    DynamoStrings.FirstOrDefaultWithUserLimitNotSupported);

            var unsafeFilteredQueriesAllowed = selectExpression.UnsafeFilteredQueriesAllowed
                || options?.AllowUnsafeFilteredQueries == true;

            if (!unsafeFilteredQueriesAllowed)
                ValidateFirstTerminalSafePath(
                    selectExpression,
                    queryConstraints,
                    effectiveSortKeyAttr,
                    isBaseTableSource);
        }

        if (selectExpression.IsSingleTerminal)
        {
            if (selectExpression.HasUserLimit)
                throw new InvalidOperationException(
                    DynamoStrings.SingleOrDefaultWithUserLimitNotSupported);

            ValidateSingleTerminalSafePath(
                selectExpression,
                queryConstraints,
                effectiveSortKeyAttr,
                isBaseTableSource);
        }

        return query;
    }

    /// <summary>Classifies the finalized active query source as keyed or scan-like.</summary>
    private static DynamoScanQueryClassification ClassifyScanQuery(
        SelectExpression selectExpression,
        DynamoQueryConstraints? queryConstraints,
        string? effectiveSortKeyAttributeName,
        bool isBaseTableSource)
    {
        var sourceDescription = isBaseTableSource
            ? "base table"
            : $"index '{selectExpression.IndexName}'";

        // Permissive fallback: if runtime metadata is unavailable (e.g. design-time or no
        // runtime model), skip scan classification rather than blocking the query.
        if (queryConstraints is null)
            return CreateScanClassification(
                selectExpression.TableName,
                sourceDescription,
                false,
                "runtime table metadata was unavailable");

        var effectivePk = selectExpression.EffectivePartitionKeyPropertyNames.FirstOrDefault();
        if (effectivePk is null)
            return CreateScanClassification(
                selectExpression.TableName,
                sourceDescription,
                false,
                "runtime key metadata was unavailable");

        var hasPkEquality = queryConstraints.EqualityConstraints.ContainsKey(effectivePk);
        var hasPkIn = queryConstraints.InConstraints.ContainsKey(effectivePk);

        if (!hasPkEquality && !hasPkIn)
            return CreateScanClassification(
                selectExpression.TableName,
                sourceDescription,
                true,
                $"missing equality or IN predicate on partition key '{effectivePk}'");

        if (selectExpression.Predicate is not null
            && PredicateHasOrTouchingKey(
                selectExpression.Predicate,
                effectivePk,
                effectiveSortKeyAttributeName))
            return CreateScanClassification(
                selectExpression.TableName,
                sourceDescription,
                true,
                "OR predicate references a partition or sort key");

        if (effectiveSortKeyAttributeName is not null
            && selectExpression.Predicate is not null
            && PredicateReferencesAttribute(
                selectExpression.Predicate,
                effectiveSortKeyAttributeName)
            && !queryConstraints.SkKeyConditions.ContainsKey(effectiveSortKeyAttributeName))
            return CreateScanClassification(
                selectExpression.TableName,
                sourceDescription,
                true,
                $"sort key '{effectiveSortKeyAttributeName}' is used as a filter instead of a single key condition");

        return CreateScanClassification(
            selectExpression.TableName,
            sourceDescription,
            false,
            hasPkIn ? "partition-key IN" : "single partition-key equality");
    }

    /// <summary>Creates a scan classification with the standard user-facing message.</summary>
    private static DynamoScanQueryClassification CreateScanClassification(
        string tableName,
        string sourceDescription,
        bool isScanLike,
        string reason)
    {
        var message = isScanLike
            ? $"Scan-like DynamoDB query detected for table '{tableName}' on {sourceDescription}: "
            + $"{reason}. Add an equality or IN predicate on the active partition key and at most one "
            + "sort-key key condition, configure ConfigureWarnings for DynamoEventId.ScanLikeQueryDetected, "
            + "or append .AllowScan() for an intentional per-query scan."
            : string.Empty;

        return new DynamoScanQueryClassification(isScanLike, sourceDescription, reason, message);
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

    /// <summary>Resolves the finalized source kind for the selected source.</summary>
    private static DynamoIndexSourceKind? ResolveIndexSourceKind(
        IReadOnlyList<DynamoIndexDescriptor> candidates,
        string? selectedIndexName)
    {
        var descriptor = selectedIndexName is null
            ? candidates.FirstOrDefault(d => d.IndexName is null)
            : candidates.FirstOrDefault(d => d.IndexName == selectedIndexName);

        return descriptor?.Kind;
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

        // ── 2. Multi-partition leading-PK check ────────────────────────────────
        // When the query spans multiple partitions (PK IN (...)), DynamoDB requires the partition
        // key to lead the ORDER BY chain. Ordering by SK first (or any non-PK attribute first) is
        // not supported across partitions.
        var isMultiPartition = pkNames.Any(pk => queryConstraints.InConstraints.ContainsKey(pk));
        if (isMultiPartition)
        {
            var firstOrdering = selectExpression.Orderings[0];
            if (firstOrdering.Expression is not SqlPropertyExpression firstProp
                || !pkNames.Contains(firstProp.PropertyName))
                throw new InvalidOperationException(
                    $"ORDER BY with a multi-partition query (WHERE partition key IN (...)) requires "
                    + $"the partition key to appear first in the ORDER BY chain on table "
                    + $"'{selectExpression.TableName}'. Use .OrderBy(e => e.PartitionKey) or "
                    + ".OrderBy(e => e.PartitionKey).ThenBy(e => e.SortKey).");
        }

        // ── 3. Partition-key constraint check ─────────────────────────────────
        // DynamoDB requires the partition key to be equality or IN-constrained when ORDER BY is
        // used.
        foreach (var pkAttr in pkNames)
            if (queryConstraints.EqualityConstraints.ContainsKey(pkAttr)
                || queryConstraints.InConstraints.ContainsKey(pkAttr))
                return;

        throw new InvalidOperationException(
            $"ORDER BY requires an equality or IN constraint on the partition key in the WHERE "
            + $"clause for table '{selectExpression.TableName}'. DynamoDB only supports ordering "
            + "within a single partition. Add a WHERE predicate on the partition key "
            + "(e.g., .Where(e => e.PartitionKey == value)).");
    }

    /// <summary>
    ///     Enforces the <c>First*</c> safe-path contract: <c>First*</c> is restricted to key-only
    ///     query shapes with no user-specified limit. Any unsafe path throws, directing the user
    ///     to <c>.AsAsyncEnumerable().FirstOrDefaultAsync()</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Safe path conditions — all must hold:
    ///         <list type="number">
    ///             <item>No user-specified <c>Limit(n)</c>.</item>
    ///             <item>PK equality constraint (not IN) in the WHERE clause.</item>
    ///             <item>All predicates use only partition-key or sort-key attributes.</item>
    ///         </list>
    ///         Special case: when the active source has no sort key, PK equality makes every partition a
    ///         single-item set, so non-key predicates are irrelevant — always safe.
    ///     </para>
    ///     <para>
    ///         Design-time: skipped when <paramref name="queryConstraints" /> is <c>null</c> (runtime
    ///         model not yet built) to avoid false negatives during tooling.
    ///     </para>
    /// </remarks>
    private static void ValidateFirstTerminalSafePath(
        SelectExpression selectExpression,
        DynamoQueryConstraints? queryConstraints,
        string? effectiveSortKeyAttributeName,
        bool isBaseTableSource)
    {
        // Design-time: runtime model unavailable, skip to avoid false negatives.
        if (queryConstraints is null)
            return;

        var pkNames = selectExpression.EffectivePartitionKeyPropertyNames;
        if (pkNames.Count == 0)
            return; // No descriptor resolved (edge case) — skip silently.

        var effectivePk = pkNames.First();

        // Condition: PK equality constraint exists (IN is not equality).
        var hasPkEquality = queryConstraints.EqualityConstraints.ContainsKey(effectivePk);

        // Condition: no non-key predicates (walk predicate tree against effective keys).
        var hasNonKeyPredicate = selectExpression.Predicate is not null
            && HasNonKeyPredicates(
                selectExpression.Predicate,
                effectivePk,
                effectiveSortKeyAttributeName);
        var hasDiscriminatorPredicate = selectExpression.Predicate is not null
            && ContainsDiscriminatorPredicate(selectExpression.Predicate);

        if (hasPkEquality && !hasNonKeyPredicate)
        {
            // Provider-injected discriminator filters are safe only when the query shape is
            // guaranteed to evaluate at most one base-table item before filtering. Otherwise,
            // DynamoDB can evaluate one item that fails discriminator filtering and still expose a
            // continuation token, causing First* to return null/throw while a matching typed row
            // exists later in the partition.
            if (hasDiscriminatorPredicate
                && !IsSingleItemBaseTableLookup(
                    queryConstraints,
                    effectivePk,
                    effectiveSortKeyAttributeName,
                    isBaseTableSource))
                throw new InvalidOperationException(
                    DynamoStrings.FirstOrDefaultRequiresKeyOnlyPath(
                        "discriminator filter on a multi-item source"));

            // Guard: if the predicate references the sort key but the sort key is not in
            // SkKeyConditions, it is a filter expression (e.g. SK IN (...) or SK = A OR SK = B),
            // not a DynamoDB key condition. DynamoDB's Limit counts *evaluated* items, not
            // matched items, so Limit=1 on a filter predicate can silently miss matching rows
            // later in the partition.
            if (effectiveSortKeyAttributeName is not null
                && selectExpression.Predicate is not null
                && PredicateReferencesAttribute(
                    selectExpression.Predicate,
                    effectiveSortKeyAttributeName)
                && !queryConstraints.SkKeyConditions.ContainsKey(effectiveSortKeyAttributeName))
                throw new InvalidOperationException(
                    DynamoStrings.FirstOrDefaultRequiresKeyOnlyPath(
                        "sort-key filter predicate (SK IN and SK OR are filter expressions, "
                        + "not DynamoDB key conditions)"));

            return; // Key-only path — safe.
        }

        // Unsafe path: non-key predicate, scan-like, or PK IN. Always throw — use
        // .AsAsyncEnumerable().FirstOrDefaultAsync(ct) for client-side selection.
        var shape = !hasPkEquality
            ? "scan-like path (no partition-key equality)"
            : "non-key predicate";
        throw new InvalidOperationException(DynamoStrings.FirstOrDefaultRequiresKeyOnlyPath(shape));
    }

    private static void ValidateSingleTerminalSafePath(
        SelectExpression selectExpression,
        DynamoQueryConstraints? queryConstraints,
        string? effectiveSortKeyAttributeName,
        bool isBaseTableSource)
    {
        // Design-time: runtime model unavailable, skip to avoid false negatives.
        if (queryConstraints is null)
            return;

        var pkNames = selectExpression.EffectivePartitionKeyPropertyNames;
        if (pkNames.Count == 0)
            return; // No descriptor resolved (edge case) — skip silently.

        var effectivePk = pkNames.First();

        // Single* permits partition-key equality or IN. PK IN is still a key condition in
        // DynamoDB PartiQL and Limit=2 is enough to surface duplicate matching rows in one page.
        var hasPkKeyCondition = queryConstraints.EqualityConstraints.ContainsKey(effectivePk)
            || queryConstraints.InConstraints.ContainsKey(effectivePk);

        var hasNonKeyPredicate = selectExpression.Predicate is not null
            && HasNonKeyPredicates(
                selectExpression.Predicate,
                effectivePk,
                effectiveSortKeyAttributeName);
        var hasDiscriminatorPredicate = selectExpression.Predicate is not null
            && ContainsDiscriminatorPredicate(selectExpression.Predicate);

        if (hasPkKeyCondition && !hasNonKeyPredicate)
        {
            if (selectExpression.Predicate is not null
                && PredicateHasOrTouchingKey(
                    selectExpression.Predicate,
                    effectivePk,
                    effectiveSortKeyAttributeName))
                throw new InvalidOperationException(
                    DynamoStrings.SingleOrDefaultRequiresKeyOnlyPath(
                        "OR predicate references a partition or sort key"));

            if (hasDiscriminatorPredicate
                && !IsSingleItemBaseTableLookup(
                    queryConstraints,
                    effectivePk,
                    effectiveSortKeyAttributeName,
                    isBaseTableSource))
                throw new InvalidOperationException(
                    DynamoStrings.SingleOrDefaultRequiresKeyOnlyPath(
                        "discriminator filter on a multi-item source"));

            if (effectiveSortKeyAttributeName is not null
                && selectExpression.Predicate is not null
                && PredicateReferencesAttribute(
                    selectExpression.Predicate,
                    effectiveSortKeyAttributeName)
                && !queryConstraints.SkKeyConditions.ContainsKey(effectiveSortKeyAttributeName))
                throw new InvalidOperationException(
                    DynamoStrings.SingleOrDefaultRequiresKeyOnlyPath(
                        "sort-key filter predicate (SK IN and SK OR are filter expressions, "
                        + "not DynamoDB key conditions)"));

            return;
        }

        var shape = !hasPkKeyCondition
            ? "scan-like path (no partition-key equality or IN)"
            : "non-key predicate";
        throw new InvalidOperationException(
            DynamoStrings.SingleOrDefaultRequiresKeyOnlyPath(shape));
    }

    private static bool HasNonKeyPredicates(
        SqlExpression predicate,
        string effectivePk,
        string? effectiveSortKey)
        => DynamoPredicateAnalysis.HasNonKeyPredicates(predicate, effectivePk, effectiveSortKey);

    private static bool PredicateReferencesAttribute(SqlExpression expression, string attributeName)
        => DynamoPredicateAnalysis.PredicateReferencesAttribute(expression, attributeName);

    private static bool PredicateHasOrTouchingKey(
        SqlExpression expression,
        string effectivePk,
        string? effectiveSortKey)
        => DynamoPredicateAnalysis.PredicateHasOrTouchingKey(
            expression,
            effectivePk,
            effectiveSortKey);

    private static bool ContainsDiscriminatorPredicate(SqlExpression expression)
        => DynamoPredicateAnalysis.ContainsDiscriminatorPredicate(expression);

    /// <summary>
    ///     Determines whether a terminal query is guaranteed to evaluate at most one base-table item
    ///     before discriminator filtering.
    /// </summary>
    private static bool IsSingleItemBaseTableLookup(
        DynamoQueryConstraints queryConstraints,
        string effectivePk,
        string? effectiveSortKeyAttributeName,
        bool isBaseTableSource)
    {
        if (!isBaseTableSource)
            return false;

        if (!queryConstraints.EqualityConstraints.ContainsKey(effectivePk))
            return false;

        if (effectiveSortKeyAttributeName is null)
            return true;

        return queryConstraints.SkKeyConditions.TryGetValue(
                effectiveSortKeyAttributeName,
                out var sortKeyConstraint)
            && sortKeyConstraint.Operator == SkOperator.Equal;
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
