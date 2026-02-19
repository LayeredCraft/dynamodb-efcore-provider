using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>Represents a SELECT query expression for DynamoDB PartiQL.</summary>
public class SelectExpression(string tableName) : Expression
{
    private static readonly MethodInfo MinMethod = ((Func<int, int, int>)Math.Min).Method;

    private readonly List<OrderingExpression> _orderings = [];
    private readonly List<ProjectionExpression> _projection = [];

    private IDictionary<ProjectionMember, Expression> _projectionMapping =
        new Dictionary<ProjectionMember, Expression>();

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <summary>The ORDER BY clauses.</summary>
    public IReadOnlyList<OrderingExpression> Orderings => _orderings;

    /// <summary>The WHERE clause predicate, or null if no filtering is applied.</summary>
    public SqlExpression? Predicate { get; private set; }

    /// <summary>
    ///     The maximum number of results to return to the caller (e.g., 1 for First, N for Take(N)).
    ///     Controls when the query stops returning items. Null means unlimited results.
    /// </summary>
    public int? ResultLimit { get; private set; }

    /// <summary>
    ///     The expression for the result limit (handles parameterized Take count). If set, this takes
    ///     precedence over ResultLimit during query execution.
    /// </summary>
    public Expression? ResultLimitExpression { get; private set; }

    /// <summary>
    ///     The maximum number of items DynamoDB should evaluate per request. Maps to
    ///     ExecuteStatementRequest.Limit. Null means no limit (DynamoDB default of 1MB).
    /// </summary>
    public int? PageSize { get; private set; }

    /// <summary>
    ///     The expression for the page size (handles parameterized WithPageSize). If set, this takes
    ///     precedence over PageSize during query execution.
    /// </summary>
    public Expression? PageSizeExpression { get; private set; }

    /// <summary>
    ///     The maximum number of items to evaluate per request. For backward compatibility -
    ///     redirects to PageSize.
    /// </summary>
    public int? Limit
    {
        get => PageSize;
        private set => PageSize = value;
    }

    /// <summary>
    ///     The list of projected columns for the SELECT clause. Must have at least one projection -
    ///     SELECT * is not supported.
    /// </summary>
    public IReadOnlyList<ProjectionExpression> Projection => _projection;

    /// <summary>The name of the DynamoDB table to query.</summary>
    public string TableName { get; } = tableName;

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

    /// <summary>Sets the maximum number of results to return to the caller.</summary>
    public void ApplyResultLimit(int? limit)
        => SetResultLimitExpression(limit is null ? null : Constant(limit.Value));

    /// <summary>Sets the result limit expression (for parameterized Take).</summary>
    public void ApplyResultLimitExpression(Expression limitExpression)
        => SetResultLimitExpression(limitExpression);

    /// <summary>
    ///     Sets or combines the result limit expression. This composes multiple row-limiting
    ///     operations (e.g. Take/First) using minimum semantics.
    /// </summary>
    public void ApplyOrCombineResultLimitExpression(Expression limitExpression)
        => ApplyOrCombineResultLimitCore(limitExpression);

    /// <summary>
    ///     Replaces the current result limit expression without combining. Used during query
    ///     compilation when normalizing expressions for execution.
    /// </summary>
    public void SetResultLimitExpression(Expression? limitExpression)
    {
        ResultLimitExpression = limitExpression;
        ResultLimit =
            limitExpression is not null && TryGetIntConstant(limitExpression, out var value)
                ? value
                : null;
    }

    private void ApplyOrCombineResultLimitCore(Expression limitExpression)
    {
        var normalizedNewLimit = ConvertToInt(limitExpression);
        var existing = ResultLimitExpression
            ?? (ResultLimit.HasValue ? Constant(ResultLimit.Value) : null);

        Expression effectiveLimit;

        if (existing is null)
            effectiveLimit = normalizedNewLimit;
        else if (TryGetIntConstant(existing, out var existingValue)
            && TryGetIntConstant(normalizedNewLimit, out var newValue))
            effectiveLimit = Constant(Math.Min(existingValue, newValue));
        else
            effectiveLimit = Call(MinMethod, ConvertToInt(existing), normalizedNewLimit);

        ResultLimitExpression = effectiveLimit;
        ResultLimit = TryGetIntConstant(effectiveLimit, out var value) ? value : null;
    }

    private static Expression ConvertToInt(Expression expression)
        => expression.Type == typeof(int) ? expression : Convert(expression, typeof(int));

    private static bool TryGetIntConstant(Expression expression, out int value)
    {
        if (expression is ConstantExpression { Value: int intValue })
        {
            value = intValue;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Sets the maximum number of items DynamoDB should evaluate per request.</summary>
    public void ApplyPageSize(int? pageSize)
    {
        PageSize = pageSize;
        PageSizeExpression = null;
    }

    /// <summary>Sets the page size expression (for parameterized WithPageSize).</summary>
    public void ApplyPageSizeExpression(Expression pageSizeExpression)
    {
        PageSizeExpression = pageSizeExpression;
        PageSize = null;
    }

    /// <summary>
    ///     Sets the maximum number of items to evaluate per request. For backward compatibility -
    ///     redirects to ApplyPageSize.
    /// </summary>
    public void ApplyLimit(int? limit) => ApplyPageSize(limit);

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

                foreach (var property in entityProjection.EntityType.GetProperties())
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
            }
            else
            {
                // Regular SQL expression
                var sqlExpr = (SqlExpression)expression;
                var alias = projectionMember.Last?.Name;
                if (string.IsNullOrEmpty(alias) && sqlExpr is SqlPropertyExpression propExpr)
                    alias = propExpr.PropertyName;

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
