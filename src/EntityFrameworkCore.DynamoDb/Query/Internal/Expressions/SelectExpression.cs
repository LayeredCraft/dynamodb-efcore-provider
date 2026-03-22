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
    ///     Adds an embedded collection attribute to the SELECT projection by name. Used for owned
    ///     collection attributes where no scalar type mapping is needed.
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
        var topLevelOwnedNameCache = new Dictionary<IEntityType, HashSet<string>>();
        var nestedOwnedNameCache = new Dictionary<IEntityType, HashSet<string>>();
        var nestedOwnedContainingAttributeNames =
            GetNestedOwnedContainingAttributeNamesFromEntityProjections(nestedOwnedNameCache);
        foreach (var (projectionMember, expression) in _projectionMapping)
            // Handle entity projections specially - expand into individual properties
            if (expression is DynamoEntityProjectionExpression entityProjection)
            {
                var topLevelOwnedContainingAttributeNames =
                    GetTopLevelOwnedContainingAttributeNames(
                        entityProjection.EntityType,
                        topLevelOwnedNameCache);

                foreach (var property in OwnedProjectionMetadata.GetTopLevelProjectionProperties(
                    entityProjection.EntityType))
                {
                    if (!OwnedProjectionMetadata.ShouldProjectTopLevelProperty(
                        entityProjection.EntityType,
                        property,
                        topLevelOwnedContainingAttributeNames))
                        continue;

                    var sqlExpr = entityProjection.BindProperty(property);
                    var memberInfo = DynamoEntityProjectionExpression.GetMemberInfo(property);
                    var propertyMember = projectionMember.Append(memberInfo);

                    var index = AddProjectionIfNotExists(sqlExpr, memberInfo.Name);
                    result[propertyMember] = Constant(index);
                }

                foreach (var containingAttributeName in topLevelOwnedContainingAttributeNames)
                    AddProjectionIfNotExists(
                        new SqlPropertyExpression(containingAttributeName, typeof(object), null),
                        containingAttributeName);

                AddDiscriminatorToProjection(entityProjection);
            }
            else
            {
                // Regular SQL expression
                var sqlExpr = (SqlExpression)expression;
                var alias = projectionMember.Last?.Name;
                if (string.IsNullOrEmpty(alias) && sqlExpr is SqlPropertyExpression propExpr)
                    alias = propExpr.PropertyName;
                if (string.IsNullOrEmpty(alias)
                    && sqlExpr is DynamoObjectAccessExpression objAccessExpr)
                    alias = objAccessExpr.PropertyName;

                if (!string.IsNullOrEmpty(alias)
                        && nestedOwnedContainingAttributeNames.Contains(alias))
                    // Root entity expansion already projects nested-owned containers; adding them
                    // again from scalar projection paths would duplicate columns and shift
                    // ordinals.
                    continue;

                var index = AddProjectionIfNotExists(sqlExpr, alias ?? "");
                result[projectionMember] = Constant(index);
            }

        _projectionMapping = result;
    }

    /// <summary>Adds the discriminator attribute to projection when configured for the entity type.</summary>
    /// <remarks>
    ///     The discriminator is often a shadow property and has no CLR member, so it cannot flow
    ///     through the normal projection-member path.
    /// </remarks>
    private void AddDiscriminatorToProjection(DynamoEntityProjectionExpression entityProjection)
    {
        var discriminatorProperty = entityProjection.EntityType.FindDiscriminatorProperty();
        if (discriminatorProperty is null)
            return;

        AddProjectionIfNotExists(
            entityProjection.BindProperty(discriminatorProperty),
            discriminatorProperty.GetAttributeName());
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
            if (string.Equals(_projection[i].Alias, alias, StringComparison.Ordinal))
            {
                // When a typed DynamoObjectAccessExpression replaces a placeholder
                // SqlPropertyExpression(typeof(object), null) that was added by
                // AddEmbeddedAttributeToProjection, upgrade the slot in-place so the
                // removing visitor's short-circuit on DynamoObjectAccessExpression fires.
                if (sqlExpression is DynamoObjectAccessExpression
                    && _projection[i].Expression is SqlPropertyExpression
                    {
                        TypeMapping: null,
                    } placeholder
                    && placeholder.Type == typeof(object))
                    _projection[i] = new ProjectionExpression(sqlExpression, alias);

                return i;
            }

        for (var i = 0; i < _projection.Count; i++)
            if (_projection[i].Expression.Equals(sqlExpression))
                return i;

        // Add new projection
        var projection = new ProjectionExpression(sqlExpression, alias);
        var index = _projection.Count;
        _projection.Add(projection);
        return index;
    }

    /// <summary>Gets top-level owned containing-attribute names for a root entity with per-query caching.</summary>
    private static HashSet<string> GetTopLevelOwnedContainingAttributeNames(
        IEntityType entityType,
        Dictionary<IEntityType, HashSet<string>> cache)
    {
        if (cache.TryGetValue(entityType, out var names))
            return names;

        names = OwnedProjectionMetadata.GetTopLevelOwnedContainingAttributeNames(entityType);
        cache[entityType] = names;
        return names;
    }

    /// <summary>Gets nested owned containing attribute names from entity projections in the mapping.</summary>
    private HashSet<string> GetNestedOwnedContainingAttributeNamesFromEntityProjections(
        Dictionary<IEntityType, HashSet<string>> cache)
    {
        HashSet<string> nestedOwnedContainingAttributeNames = new(StringComparer.Ordinal);

        foreach (var entityProjection in _projectionMapping.Values
            .OfType<DynamoEntityProjectionExpression>())
        {
            if (entityProjection.EntityType.IsOwned())
                continue;

            if (!cache.TryGetValue(entityProjection.EntityType, out var nestedOwnedNames))
            {
                nestedOwnedNames =
                    OwnedProjectionMetadata.GetNestedOwnedContainingAttributeNames(
                        entityProjection.EntityType);
                cache[entityProjection.EntityType] = nestedOwnedNames;
            }

            foreach (var nestedOwnedName in nestedOwnedNames)
                nestedOwnedContainingAttributeNames.Add(nestedOwnedName);
        }

        return nestedOwnedContainingAttributeNames;
    }

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
        =>
            // SelectExpression is immutable from the perspective of the visitor
            this;
}
