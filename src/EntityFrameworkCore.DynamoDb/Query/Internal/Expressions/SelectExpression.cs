using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>Represents a SELECT query expression for DynamoDB PartiQL.</summary>
public class SelectExpression(string tableName, string? queryEntityTypeName = null) : Expression
{
    private readonly List<OrderingExpression> _orderings = [];
    private readonly List<ProjectionExpression> _projection = [];
    private SqlExpression? _deferredDiscriminatorPredicate;

    private IDictionary<ProjectionMember, Expression> _projectionMapping =
        new Dictionary<ProjectionMember, Expression>();

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <summary>The ORDER BY clauses.</summary>
    public IReadOnlyList<OrderingExpression> Orderings => _orderings;

    /// <summary>The WHERE clause predicate, or null if no filtering is applied.</summary>
    public SqlExpression? Predicate { get; private set; }

    /// <summary>
    ///     The user-specified evaluation limit from <c>.Limit(n)</c>. Maps directly to
    ///     <c>ExecuteStatementRequest.Limit</c>. Null means not set by the user.
    /// </summary>
    public int? Limit { get; private set; }

    /// <summary>
    ///     Supports parameterized <c>Limit(n)</c> (compiled queries / captured variables). Takes
    ///     precedence over <see cref="Limit"/> during execution normalization.
    /// </summary>
    public Expression? LimitExpression { get; private set; }

    /// <summary>
    ///     True when <c>Limit(n)</c> was explicitly called by the user, as opposed to the
    ///     implicit <c>Limit=1</c> set by <see cref="ApplyImplicitLimit"/> for key-only
    ///     <c>First*</c> queries.
    /// </summary>
    public bool HasUserLimit { get; private set; }

    /// <summary>
    ///     True when the query terminal is <c>First*</c> (<c>FirstAsync</c>,
    ///     <c>FirstOrDefaultAsync</c>). Drives implicit <c>Limit=1</c>, single-page execution,
    ///     and safe-path validation in the postprocessor.
    /// </summary>
    public bool IsFirstTerminal { get; private set; }

    /// <summary>
    ///     The optional continuation token to seed on the first DynamoDB request when
    ///     <c>.WithNextToken(...)</c> is used.
    /// </summary>
    public string? SeedNextToken { get; private set; }

    /// <summary>
    ///     Supports parameterized continuation-token expressions for compiled queries.
    ///     Takes precedence over <see cref="SeedNextToken"/> when present.
    /// </summary>
    public Expression? SeedNextTokenExpression { get; private set; }

    /// <summary>
    ///     The list of projected columns for the SELECT clause. Must have at least one projection -
    ///     SELECT * is not supported.
    /// </summary>
    public IReadOnlyList<ProjectionExpression> Projection => _projection;

    /// <summary>Stores discriminator filtering to be applied after query composition is complete.</summary>
    public void SetDeferredDiscriminatorPredicate(SqlExpression predicate)
        => _deferredDiscriminatorPredicate = predicate;

    /// <summary>Applies the deferred discriminator predicate, if present.</summary>
    public void ApplyDeferredDiscriminatorPredicate()
    {
        if (_deferredDiscriminatorPredicate is null)
            return;

        ApplyPredicate(_deferredDiscriminatorPredicate);
        _deferredDiscriminatorPredicate = null;
    }

    /// <summary>The name of the DynamoDB table to query.</summary>
    public string TableName { get; } = tableName;

    /// <summary>
    ///     The query entity type name when the query originates from an entity set. This is
    ///     preserved through projection rewrites so downstream validation can remain scoped to the
    ///     originating entity type.
    /// </summary>
    public string? QueryEntityTypeName { get; } = queryEntityTypeName;

    /// <summary>The secondary index name to query, or null for the base table.</summary>
    public string? IndexName { get; private set; }

    /// <summary>Sets the secondary index name to use in the FROM clause.</summary>
    public void ApplyIndexName(string? indexName) => IndexName = indexName;

    /// <summary>True when this query has opted into intentional scan execution.</summary>
    public bool ScanAllowed { get; private set; }

    /// <summary>Marks this query as allowed to execute even when scan-like.</summary>
    public void AllowScan() => ScanAllowed = true;

    /// <summary>The finalized scan-like query classification for this read query.</summary>
    internal DynamoScanQueryClassification? ScanQueryClassification { get; private set; }

    /// <summary>Stores the finalized scan-like query classification for runtime enforcement.</summary>
    internal void ApplyScanQueryClassification(DynamoScanQueryClassification classification)
        => ScanQueryClassification = classification;

    /// <summary>
    ///     Gets the effective partition-key property names for the finalized query source.
    ///     Contains exactly the active source partition key (base table or selected index).
    /// </summary>
    public IReadOnlySet<string> EffectivePartitionKeyPropertyNames { get; private set; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Replaces the effective partition-key property names used by downstream SQL generation.</summary>
    public void ApplyEffectivePartitionKeyPropertyNames(
        IReadOnlySet<string> effectivePartitionKeyPropertyNames)
        => EffectivePartitionKeyPropertyNames = new HashSet<string>(
            effectivePartitionKeyPropertyNames,
            StringComparer.Ordinal);

    /// <inheritdoc />
    public override Type Type => typeof(object);

    /// <summary>Applies or combines a WHERE predicate.</summary>
    public void ApplyPredicate(SqlExpression predicate)
        => Predicate =
            Predicate == null
                ? predicate
                : new SqlBinaryExpression(
                    ExpressionType.AndAlso,
                    Predicate,
                    predicate,
                    typeof(bool),
                    null);

    /// <summary>Replaces all orderings with a single ordering.</summary>
    public void ApplyOrdering(OrderingExpression ordering)
    {
        _orderings.Clear();
        _orderings.Add(ordering);
    }

    /// <summary>Appends an additional ordering (for ThenBy).</summary>
    public void AppendOrdering(OrderingExpression ordering) => _orderings.Add(ordering);

    /// <summary>
    ///     Sets the user-specified evaluation limit from <c>.Limit(n)</c>. Overwrites any previous
    ///     value — last call wins when chained. Sets <see cref="HasUserLimit"/> to <c>true</c>.
    /// </summary>
    public void ApplyUserLimit(int limit)
    {
        Limit = limit;
        LimitExpression = Constant(limit);
        HasUserLimit = true;
    }

    /// <summary>
    ///     Sets the limit expression for parameterized <c>Limit(n)</c> (runtime parameter or
    ///     compiled-query expression). Sets <see cref="HasUserLimit"/> to <c>true</c>.
    /// </summary>
    public void ApplyUserLimitExpression(Expression limitExpression)
    {
        LimitExpression = limitExpression;
        Limit = limitExpression is ConstantExpression { Value: int v } ? v : null;
        HasUserLimit = true;
    }

    /// <summary>
    ///     Sets <see cref="Limit"/> as the implicit default for key-only <c>First*</c> queries.
    ///     Does NOT set <see cref="HasUserLimit"/>. A subsequent <see cref="ApplyUserLimit"/>
    ///     call overrides this value.
    /// </summary>
    public void ApplyImplicitLimit(int limit)
    {
        // Only apply when the user has not already set an explicit limit.
        if (!HasUserLimit)
        {
            Limit = limit;
            LimitExpression = Constant(limit);
        }
    }

    /// <summary>Marks this query as having a <c>First*</c> terminal.</summary>
    public void MarkAsFirstTerminal() => IsFirstTerminal = true;

    /// <summary>
    ///     Sets the first-request continuation token from a constant value.
    /// </summary>
    public void ApplySeedNextToken(string seedNextToken)
    {
        SeedNextToken = seedNextToken;
        SeedNextTokenExpression = Constant(seedNextToken);
    }

    /// <summary>
    ///     Sets the first-request continuation token from a parameterized expression.
    /// </summary>
    public void ApplySeedNextTokenExpression(Expression seedNextTokenExpression)
    {
        SeedNextTokenExpression = seedNextTokenExpression;
        SeedNextToken = seedNextTokenExpression is ConstantExpression { Value: string token }
            ? token
            : null;
    }

    /// <summary>Adds a projection to the SELECT clause.</summary>
    public void AddToProjection(ProjectionExpression projectionExpression)
        => _projection.Add(projectionExpression);

    /// <summary>
    ///     Adds a SQL expression to the projection list if it doesn't already exist. Returns the
    ///     index of the projection (existing or newly added).
    /// </summary>
    public int AddToProjection(SqlExpression sqlExpression, string alias)
        => AddProjectionIfNotExists(sqlExpression, alias);

    /// <summary>
    ///     Adds an embedded attribute to the SELECT projection by name. Used for complex properties
    ///     and complex collections where no scalar type mapping is needed.
    /// </summary>
    public void AddEmbeddedAttributeToProjection(string attributeName)
        => AddProjectionIfNotExists(
            new SqlPropertyExpression(attributeName, typeof(object), null),
            attributeName);

    /// <summary>Clears all projections.</summary>
    public void ClearProjection() => _projection.Clear();

    /// <summary>
    ///     Replaces the projection mapping with a new mapping. Used by TranslateSelect to set custom
    ///     projections.
    /// </summary>
    public void ReplaceProjectionMapping(
        IDictionary<ProjectionMember, Expression> projectionMapping)
    {
        _projectionMapping.Clear();
        foreach (var (member, expr) in projectionMapping)
            _projectionMapping[member] = expr;
    }

    /// <summary>
    ///     Gets the mapped expression for a projection member. After ApplyProjection(), returns
    ///     Constant(index).
    /// </summary>
    public Expression GetMappedProjection(ProjectionMember member) => _projectionMapping[member];

    /// <summary>
    ///     Converts abstract projection mapping to concrete projection list. Replaces SqlExpression
    ///     values with Constant(index) references. Expands DynamoEntityProjectionExpression into
    ///     individual property projections.
    /// </summary>
    public void ApplyProjection()
    {
        // Only apply if projections haven't been finalized yet
        if (_projectionMapping.Count == 0 || _projection.Count > 0)
            return;

        var result = new Dictionary<ProjectionMember, Expression>();
        foreach (var (projectionMember, expression) in _projectionMapping)
            // Handle entity projections specially - expand into individual properties
            if (expression is DynamoEntityProjectionExpression entityProjection)
            {
                foreach (var property in GetProjectedProperties(entityProjection.EntityType))
                {
                    if (property.IsRuntimeOnly())
                        continue;

                    var sqlExpr = entityProjection.BindProperty(property);

                    // Shadow properties (no CLR member) only need to appear in the SQL SELECT
                    // list so the attribute is returned by DynamoDB. The compiled shaper reads
                    // them via the ValueBufferTryReadValue → VisitMethodCall interception path
                    // keyed by attribute name — no projection-member entry is required.
                    if (property.IsShadowProperty())
                    {
                        AddProjectionIfNotExists(sqlExpr, property.GetAttributeName());
                        continue;
                    }

                    var memberInfo = DynamoEntityProjectionExpression.GetMemberInfo(property);
                    var propertyMember = projectionMember.Append(memberInfo);

                    var index = AddProjectionIfNotExists(sqlExpr, memberInfo.Name);
                    result[propertyMember] = Constant(index);
                }

                // Add complex property map attributes so they are included in the SELECT list
                foreach (var complexProperty in GetProjectedComplexProperties(
                    entityProjection.EntityType))
                {
                    var attributeName =
                        ((IReadOnlyComplexProperty)complexProperty).GetAttributeName();
                    AddProjectionIfNotExists(
                        new SqlPropertyExpression(attributeName, typeof(object), null),
                        attributeName);
                }
            }
            else
            {
                // Regular SQL expression
                var sqlExpr = (SqlExpression)expression;
                var alias = projectionMember.Last?.Name;
                if (string.IsNullOrEmpty(alias) && sqlExpr is SqlPropertyExpression propExpr)
                    alias = propExpr.PropertyName;

                var index = AddProjectionIfNotExists(sqlExpr, alias ?? "");
                result[projectionMember] = Constant(index);
            }

        _projectionMapping = result;
    }

    /// <summary>
    ///     Adds a SQL expression to the projection list if it doesn't already exist. Returns the
    ///     index of the projection (existing or newly added).
    /// </summary>
    private int AddProjectionIfNotExists(SqlExpression sqlExpression, string alias)
    {
        // Prefer alias deduplication first so independently-built equivalent SQL expressions still
        // resolve to a stable projection ordinal for the same logical column.
        for (var i = 0; i < _projection.Count; i++)
            if (string.Equals(_projection[i].Alias, alias, StringComparison.OrdinalIgnoreCase))
                return i;

        for (var i = 0; i < _projection.Count; i++)
            if (_projection[i].Expression.Equals(sqlExpression))
                return i;

        // Add new projection
        var projection = new ProjectionExpression(sqlExpression, alias);
        var index = _projection.Count;
        _projection.Add(projection);
        return index;
    }

    /// <summary>
    ///     Gets scalar properties that must be projected for entity materialization, including
    ///     declared properties across the query-relevant inheritance hierarchy.
    /// </summary>
    private static IEnumerable<IProperty> GetProjectedProperties(IEntityType entityType)
        => GetProjectedEntityTypes(entityType)
            .SelectMany(static type => type.GetDeclaredProperties());

    /// <summary>
    ///     Gets complex properties that must be projected for entity materialization, including
    ///     declared complex properties across the query-relevant inheritance hierarchy.
    /// </summary>
    private static IEnumerable<IComplexProperty>
        GetProjectedComplexProperties(IEntityType entityType)
        => GetProjectedEntityTypes(entityType)
            .SelectMany(static type => type.GetDeclaredComplexProperties());

    /// <summary>
    ///     Gets hierarchy entity types whose declared members must be projected for the current
    ///     query entity type.
    /// </summary>
    /// <remarks>
    ///     Includes the queried entity, its base chain, and any concrete descendants (plus their
    ///     base chains) so discriminator-based materialization has every attribute needed, while
    ///     avoiding sibling-only members for leaf derived queries.
    /// </remarks>
    private static IEnumerable<IEntityType> GetProjectedEntityTypes(IEntityType entityType)
    {
        HashSet<IEntityType> projectedTypes = [];

        var concreteTypes = entityType.GetConcreteDerivedTypesInclusive().ToList();
        if (concreteTypes.Count == 0)
            concreteTypes.Add(entityType);

        foreach (var concreteType in concreteTypes)
            foreach (var hierarchyType in concreteType.GetAllBaseTypesInclusive())
                projectedTypes.Add(hierarchyType);

        return projectedTypes;
    }

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
        =>
            // SelectExpression is immutable from the perspective of the visitor
            this;
}
