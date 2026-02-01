using System.Linq.Expressions;
using System.Reflection;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Translates Select lambda bodies into projection mappings (ProjectionMember → SqlExpression).
///     Supports anonymous types, DTOs, and scalar projections.
/// </summary>
public class DynamoProjectionBindingExpressionVisitor(
    DynamoSqlTranslatingExpressionVisitor sqlTranslator,
    ISqlExpressionFactory sqlExpressionFactory) : ExpressionVisitor
{
    private SelectExpression _selectExpression = null!;
    private readonly Dictionary<ProjectionMember, Expression> _projectionMapping = new();
    private readonly Stack<ProjectionMember> _projectionMembers = new();

    /// <summary>
    ///     Translates a Select lambda body into a projection mapping and returns a shaper expression.
    /// </summary>
    public Expression Translate(SelectExpression selectExpression, Expression expression)
    {
        _selectExpression = selectExpression;
        _projectionMembers.Push(new ProjectionMember());

        // Clear any existing concrete projections - we're rebuilding from scratch
        _selectExpression.ClearProjection();

        var result = Visit(expression);
        if (result == null)
            throw new InvalidOperationException("Failed to translate Select projection.");

        _selectExpression.ReplaceProjectionMapping(_projectionMapping);
        _projectionMapping.Clear();
        _selectExpression = null!;
        _projectionMembers.Clear();

        return result;
    }

    /// <summary>
    ///     Handles anonymous type projections: new { x.Id, x.Name }
    /// </summary>
    protected override Expression VisitNew(NewExpression node)
    {
        // Parameterless constructors (e.g., new object())
        if (node.Arguments.Count == 0)
            return node;

        // Anonymous types must have members
        if (node.Members == null)
            throw new InvalidOperationException(
                $"DynamoDB provider does not support projection to type '{node.Type.Name}' without member assignments. "
                + "Use anonymous types (new {{ x.Prop }}) or DTOs with MemberInitExpression.");

        var newArguments = new Expression[node.Arguments.Count];
        for (var i = 0; i < newArguments.Length; i++)
        {
            var argument = node.Arguments[i];

            // Push member onto stack for hierarchical tracking
            var projectionMember = _projectionMembers.Peek().Append(node.Members[i]);
            _projectionMembers.Push(projectionMember);

            var visitedArgument = Visit(argument);

            // Check if translation failed
            if (visitedArgument == null)
                throw new InvalidOperationException(
                    $"Failed to translate projection argument: {argument}. "
                    + "DynamoDB PartiQL does not support computed expressions in SELECT clause. "
                    + "Only direct property access is supported.");

            _projectionMembers.Pop();

            newArguments[i] = visitedArgument;
        }

        return node.Update(newArguments);
    }

    /// <summary>
    ///     Handles DTO projections: new ItemDto { Id = x.Id, Name = x.Name }
    /// </summary>
    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
        var newExpression = Visit(node.NewExpression);
        if (newExpression == null)
            throw new InvalidOperationException("Failed to visit NewExpression in MemberInit.");

        var newBindings = new MemberBinding[node.Bindings.Count];
        for (var i = 0; i < newBindings.Length; i++)
        {
            if (node.Bindings[i].BindingType != MemberBindingType.Assignment)
                throw new InvalidOperationException(
                    "DynamoDB provider only supports MemberAssignment bindings in MemberInitExpression. "
                    + $"Binding type '{node.Bindings[i].BindingType}' is not supported.");

            var memberAssignment = (MemberAssignment)node.Bindings[i];

            // Push member onto stack
            var projectionMember = _projectionMembers.Peek().Append(memberAssignment.Member);
            _projectionMembers.Push(projectionMember);

            var visitedExpression = Visit(memberAssignment.Expression);

            if (visitedExpression == null)
                throw new InvalidOperationException(
                    $"Failed to translate member assignment for '{memberAssignment.Member.Name}'. "
                    + "DynamoDB PartiQL does not support computed expressions in SELECT clause.");

            _projectionMembers.Pop();

            newBindings[i] = memberAssignment.Update(visitedExpression);
        }

        return node.Update((NewExpression)newExpression, newBindings);
    }

    /// <summary>
    ///     Handles StructuralTypeShaperExpression (entity shapers from previous query stage).
    /// </summary>
    protected override Expression VisitExtension(Expression node)
    {
        if (node is not StructuralTypeShaperExpression entityShaperExpression)
            return base.VisitExtension(node);

        var projectionBindingExpression =
            (ProjectionBindingExpression)entityShaperExpression.ValueBufferExpression;

        // Verify it belongs to our SelectExpression
        if (projectionBindingExpression.QueryExpression != _selectExpression)
            throw new InvalidOperationException(
                "ProjectionBindingExpression belongs to a different SelectExpression.");

        if (projectionBindingExpression.ProjectionMember == null)
            throw new InvalidOperationException(
                "ProjectionBindingExpression has null ProjectionMember.");

        // Get the entity projection expression (single source of truth)
        var entityProjection =
            _selectExpression.GetMappedProjection(projectionBindingExpression.ProjectionMember);

        if (entityProjection is not DynamoEntityProjectionExpression dynamoEntityProjection)
            throw new InvalidOperationException(
                $"Expected DynamoEntityProjectionExpression but got {entityProjection.GetType().Name}");

        var entityType = entityShaperExpression.StructuralType as IEntityType;
        if (entityType == null)
            throw new InvalidOperationException(
                $"Expected IEntityType but got {entityShaperExpression.StructuralType.GetType().Name}");

        // Use entity projection to bind each property (single source of truth)
        foreach (var property in entityType.GetProperties())
        {
            var sqlProperty = dynamoEntityProjection.BindProperty(property);
            var memberInfo = DynamoEntityProjectionExpression.GetMemberInfo(property);
            var projectionMember = _projectionMembers.Peek().Append(memberInfo);
            _projectionMapping[projectionMember] = sqlProperty;
        }

        // Return a projection binding with the current member
        return new ProjectionBindingExpression(
            _selectExpression,
            _projectionMembers.Peek(),
            typeof(ValueBuffer));
    }

    /// <summary>
    ///     Handles property access expressions (e.g., item.Pk, item.Name).
    /// </summary>
    protected override Expression VisitMember(MemberExpression node)
    {
        // If it's accessing a property on an entity shaper, translate to SQL property
        // Do NOT visit the inner expression to avoid expanding all entity properties
        if (node.Expression is StructuralTypeShaperExpression)
        {
            var propertyName = node.Member.Name;
            var sqlProperty = sqlExpressionFactory.Property(propertyName, node.Type);

            // Store mapping: ProjectionMember → SqlExpression
            _projectionMapping[_projectionMembers.Peek()] = sqlProperty;

            // Return ProjectionBindingExpression as placeholder
            return new ProjectionBindingExpression(
                _selectExpression,
                _projectionMembers.Peek(),
                node.Type);
        }

        // For other member accesses, try SQL translation
        var translation = sqlTranslator.Translate(node);

        _projectionMapping[_projectionMembers.Peek()] = translation
            ?? throw new InvalidOperationException(
                $"Failed to translate member access to SQL: {node}. "
                + "DynamoDB PartiQL does not support computed expressions or method calls in SELECT clause. "
                + "Only direct property access is supported (e.g., x.Name, not x.Name.ToUpper()).");

        return new ProjectionBindingExpression(
            _selectExpression,
            _projectionMembers.Peek(),
            node.Type);
    }

    /// <summary>
    ///     Default visitor for all other expressions - attempts SQL translation.
    /// </summary>
    public override Expression? Visit(Expression? node)
    {
        if (node == null)
            return null;

        // Let specific visitor methods handle these
        if (node is NewExpression
            or MemberInitExpression
            or StructuralTypeShaperExpression
            or MemberExpression)
            return base.Visit(node);

        // Try to translate to SQL
        var translation = sqlTranslator.Translate(node);

        // Store mapping: ProjectionMember → SqlExpression
        _projectionMapping[_projectionMembers.Peek()] = translation
            ?? throw new InvalidOperationException(
                $"Failed to translate expression to SQL: {node}. "
                + "DynamoDB PartiQL does not support computed expressions or method calls in SELECT clause. "
                + "Only direct property access is supported (e.g., x.Name, not x.Name.ToUpper()).");

        // Return ProjectionBindingExpression as placeholder
        return new ProjectionBindingExpression(
            _selectExpression,
            _projectionMembers.Peek(),
            node.Type);
    }
}
