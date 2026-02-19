using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
///     Represents an entity projection expression that lazily binds properties on-demand.
///     This ensures a single source of truth for entity-to-SQL property mapping.
/// </summary>
public class DynamoEntityProjectionExpression : SqlExpression
{
    private readonly Dictionary<IProperty, SqlExpression> _propertyExpressionsMap = new();
    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    /// <summary>
    ///     Creates a new instance of <see cref="DynamoEntityProjectionExpression"/>.
    /// </summary>
    /// <param name="entityType">The entity type being projected.</param>
    /// <param name="sqlExpressionFactory">Factory for creating SQL expressions.</param>
    public DynamoEntityProjectionExpression(
        IEntityType entityType,
        ISqlExpressionFactory sqlExpressionFactory) : base(entityType.ClrType, null)
    {
        EntityType = entityType;
        _sqlExpressionFactory = sqlExpressionFactory;
    }

    /// <summary>
    ///     The entity type being projected.
    /// </summary>
    public IEntityType EntityType { get; }

    /// <summary>
    ///     Binds a property to its SQL expression, creating it on-demand and caching the result.
    ///     This is the single source of truth for property-to-SQL mapping.
    /// </summary>
    /// <param name="property">The property to bind.</param>
    /// <returns>The SQL expression representing the property.</returns>
    public SqlExpression BindProperty(IProperty property)
    {
        if (!EntityType.IsAssignableFrom(property.DeclaringType)
            && !property.DeclaringType.IsAssignableFrom(EntityType))
            throw new InvalidOperationException(
                $"Unable to bind property '{property.Name}' to entity projection for '{EntityType.DisplayName()}'. "
                + $"Property belongs to '{property.DeclaringType.DisplayName()}'.");

        if (!_propertyExpressionsMap.TryGetValue(property, out var expression))
        {
            // Create SQL property expression on-demand
            var propertyName = property.GetAttributeName();
            expression = _sqlExpressionFactory.Property(propertyName, property.ClrType);
            _propertyExpressionsMap[property] = expression;
        }

        return expression;
    }

    /// <summary>
    ///     Gets the member info (PropertyInfo or FieldInfo) for a property.
    /// </summary>
    /// <param name="property">The property to get member info for.</param>
    /// <returns>The MemberInfo for the property.</returns>
    public static MemberInfo GetMemberInfo(IProperty property)
    {
        var memberInfo = (MemberInfo?)property.PropertyInfo ?? property.FieldInfo;
        if (memberInfo == null)
            throw new InvalidOperationException(
                $"Property '{property.Name}' has no PropertyInfo or FieldInfo.");

        return memberInfo;
    }

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
        =>
            // Entity projection expressions are immutable from visitor perspective
            // Property expressions are created on-demand, not stored as children
            this;

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping)
        =>
            // Entity projections don't have a direct type mapping
            // They represent the entire entity, not a single value
            this;

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
        => expressionPrinter.Append($"DynamoEntityProjection({EntityType.DisplayName()})");

    /// <inheritdoc />
    public override string ToString() => $"DynamoEntityProjection({EntityType.DisplayName()})";
}
