using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Snapshot of compile-time state supplied to <see cref="IDynamoIndexSelectionAnalyzer.Analyze"/>.
/// </summary>
/// <remarks>
/// Candidates are pre-fetched from the <c>Dynamo:RuntimeTableModel</c> runtime annotation by
/// <see cref="DynamoQueryTranslationPostprocessor"/> once per query, so the analyzer never
/// re-reads model annotations or rebuilds metadata per property lookup.
/// </remarks>
internal sealed class DynamoIndexAnalysisContext
{
    /// <summary>
    /// The fully-finalised <see cref="SelectExpression"/> for the query.
    /// Deferred discriminator predicates and projections have already been applied before the
    /// context is passed to the analyzer, so the analyzer sees the complete predicate tree.
    /// </summary>
    public required SelectExpression SelectExpression { get; init; }

    /// <summary>
    /// The explicit secondary-index name supplied via <c>.WithIndex()</c>, or <c>null</c> when
    /// the caller did not request a specific index.
    /// </summary>
    public required string? ExplicitIndexHint { get; init; }

    /// <summary>
    /// All available query sources (base table first, then secondary indexes in registration order)
    /// for the queried entity type, pre-fetched from the <see cref="DynamoRuntimeTableModel"/>
    /// runtime annotations. Empty when the runtime model has not been initialised (design-time).
    /// </summary>
    public required IReadOnlyList<DynamoIndexDescriptor> CandidateDescriptors { get; init; }

    /// <summary>
    /// The name of the entity type being queried, used for shared-table validation scoping.
    /// <c>null</c> for non-entity projection queries; the analyzer falls back to searching all
    /// candidates in this case.
    /// </summary>
    public required string? QueryEntityTypeName { get; init; }

    /// <summary>
    /// Structural key-condition constraints extracted from the finalized predicate by
    /// <see cref="DynamoConstraintExtractionVisitor"/>. Null when no candidates are available
    /// at design-time. Step 8 uses this to evaluate candidate descriptors for auto-selection.
    /// </summary>
    /// <remarks>
    /// TODO(partial-projection): extend with projection shape analysis once non-ALL index
    /// projections are supported.
    /// </remarks>
    public DynamoQueryConstraints? QueryConstraints { get; init; }

    /// <summary>
    /// The configured automatic index selection mode, read from
    /// <see cref="DynamoDbOptionsExtension.AutomaticIndexSelectionMode"/> at compile time.
    /// </summary>
    public DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode { get; init; }

    /// <summary>
    /// <c>true</c> when <c>.WithoutIndex()</c> was called on this query. The analyzer will
    /// suppress all index selection and emit <c>DYNAMO_IDX006</c>. Combining this with a
    /// non-null <see cref="ExplicitIndexHint"/> is a programmer error and throws at compile time.
    /// </summary>
    public bool IndexSelectionDisabled { get; init; }
}
