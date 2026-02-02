using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>Represents a SELECT query expression for DynamoDB PartiQL.</summary>
public class SelectExpression(string tableName) : Expression
{
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

    /// <summary>Adds a projection to the SELECT clause.</summary>
    public void AddToProjection(ProjectionExpression projectionExpression)
        => _projection.Add(projectionExpression);

    /// <summary>
    ///     Adds a SQL expression to the projection list if it doesn't already exist. Returns the
    ///     index of the projection (existing or newly added).
    /// </summary>
    public int AddToProjection(SqlExpression sqlExpression, string alias)
        => AddProjectionIfNotExists(sqlExpression, alias);

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
                foreach (var property in entityProjection.EntityType.GetProperties())
                {
                    var sqlExpr = entityProjection.BindProperty(property);
                    var memberInfo = DynamoEntityProjectionExpression.GetMemberInfo(property);
                    var propertyMember = projectionMember.Append(memberInfo);

                    var index = AddProjectionIfNotExists(sqlExpr, memberInfo.Name);
                    result[propertyMember] = Constant(index);
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
        // Check if we already have this expression in the projection list (deduplicate)
        for (var i = 0; i < _projection.Count; i++)
            if (_projection[i].Expression.Equals(sqlExpression))
                return i;

        // Add new projection
        var projection = new ProjectionExpression(sqlExpression, alias);
        var index = _projection.Count;
        _projection.Add(projection);
        return index;
    }

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
        =>
            // SelectExpression is immutable from the perspective of the visitor
            this;
}
