using System.Linq.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// DynamoDB provider post-processor that finalises the query expression and performs index
/// selection before SQL generation.
/// </summary>
/// <remarks>
/// <para>
/// Follows the same pattern as <c>CosmosQueryTranslationPostprocessor</c>:
/// <list type="number">
///   <item><description>Calls <c>base.Process</c> to apply standard EF post-processing.</description></item>
///   <item><description>Finalises the <see cref="SelectExpression"/> (discriminator predicate and projection)
///     so the analyzer can inspect the complete predicate tree and projection shape.</description></item>
///   <item><description>Pre-fetches runtime index descriptors from model annotations (once per compile).</description></item>
///   <item><description>Delegates to <see cref="IDynamoIndexSelectionAnalyzer"/> to choose an index.</description></item>
///   <item><description>Applies the chosen index name to the <see cref="SelectExpression"/> so the SQL generator
///     emits <c>FROM "Table"."Index"</c> or <c>FROM "Table"</c> accordingly.</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class DynamoQueryTranslationPostprocessor(
    QueryTranslationPostprocessorDependencies dependencies,
    DynamoQueryCompilationContext dynamoQueryCompilationContext,
    IDynamoIndexSelectionAnalyzer indexSelectionAnalyzer)
    : QueryTranslationPostprocessor(dependencies, dynamoQueryCompilationContext)
{
    /// <summary>
    /// Applies standard post-processing, finalises the <see cref="SelectExpression"/>, runs
    /// index-selection analysis, and applies the chosen index name to the query model.
    /// </summary>
    /// <param name="query">The translated LINQ expression tree to post-process.</param>
    /// <returns>
    /// The post-processed expression tree with <see cref="SelectExpression.IndexName"/> set to
    /// the analyzer's chosen index, or <c>null</c> for base-table queries.
    /// </returns>
    public override Expression Process(Expression query)
    {
        query = base.Process(query);

        if (query is not ShapedQueryExpression { QueryExpression: SelectExpression selectExpression })
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

        var analysisCtx = new DynamoIndexAnalysisContext
        {
            SelectExpression     = selectExpression,
            ExplicitIndexHint    = dynamoQueryCompilationContext.ExplicitIndexName,
            CandidateDescriptors = candidates,
            QueryEntityTypeName  = selectExpression.QueryEntityTypeName,
            QueryConstraints     = queryConstraints,
        };

        // IDynamoIndexSelectionAnalyzer is a DI singleton injected via the factory, so callers can
        // replace it with ReplaceService<IDynamoIndexSelectionAnalyzer, TCustom>() for steps 7–8
        // auto-selection or for test-time substitution.
        var decision = indexSelectionAnalyzer.Analyze(analysisCtx);

        if (decision.SelectedIndexName is { } chosen)
            selectExpression.ApplyIndexName(chosen);

        selectExpression.ApplyEffectivePartitionKeyPropertyNames(
            ResolveEffectivePartitionKeyPropertyNames(candidates, decision.SelectedIndexName));

        // step 10 will wire decision.Diagnostics to EF structured logger events.

        return query;
    }

    /// <summary>
    /// Resolves the pre-fetched list of query source candidates for the given table and entity
    /// type from the <see cref="DynamoRuntimeTableModel"/> runtime annotation.
    /// </summary>
    /// <param name="runtimeModel">
    /// The runtime model, or <c>null</c> when the model has not yet been initialised (design-time).
    /// </param>
    /// <param name="tableGroupName">Physical DynamoDB table name from the <see cref="SelectExpression"/>.</param>
    /// <param name="queryEntityTypeName">
    /// The CLR name of the entity type being queried, used to scope candidates for shared-table
    /// models. When <c>null</c> (non-entity projection queries), all sources for the table are
    /// returned deduplicated by index name.
    /// </param>
    /// <returns>
    /// The scoped list of <see cref="DynamoIndexDescriptor"/> candidates (base table first, then
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
        if (queryEntityTypeName is not null
            && tableDescriptor.SourcesByQueryEntityTypeName.TryGetValue(
                queryEntityTypeName,
                out var scopedSources))
            return scopedSources;

        // Non-entity projection queries have no entity type name; fall back to a deduplicated
        // union of all sources so explicit-hint validation can still succeed.
        return tableDescriptor.SourcesByQueryEntityTypeName.Values
            .SelectMany(v => v)
            .DistinctBy(d => d.IndexName)
            .ToList();
    }

    /// <summary>Resolves effective partition-key property names for the finalized query source.</summary>
    /// <param name="candidates">The scoped runtime source descriptors for the query.</param>
    /// <param name="selectedIndexName">The selected index name, or <c>null</c> for base table.</param>
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
}
