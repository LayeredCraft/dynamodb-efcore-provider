using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Translates Select lambda bodies into projection mappings (ProjectionMember →
///     SqlExpression). Supports anonymous types, DTOs, and scalar projections.
/// </summary>
public class DynamoProjectionBindingExpressionVisitor(
    DynamoSqlTranslatingExpressionVisitor sqlTranslator,
    ISqlExpressionFactory sqlExpressionFactory) : ExpressionVisitor
{
    private readonly Dictionary<ProjectionMember, Expression> _projectionMapping = new();
    private readonly Stack<ProjectionMember> _projectionMembers = new();
    private bool _indexBasedBinding;
    private SelectExpression _selectExpression = null!;

    /// <summary>Translates a Select lambda body into a projection mapping and returns a shaper expression.</summary>
    public Expression Translate(SelectExpression selectExpression, Expression expression)
    {
        _selectExpression = selectExpression;
        _projectionMembers.Push(new ProjectionMember());

        // Clear any existing concrete projections - we're rebuilding from scratch
        _selectExpression.ClearProjection();

        _indexBasedBinding = false;
        var result = Visit(expression);

        if (result == QueryCompilationContext.NotTranslatedExpression)
        {
            _projectionMapping.Clear();
            _projectionMembers.Clear();
            _projectionMembers.Push(new ProjectionMember());
            _selectExpression.ClearProjection();
            _indexBasedBinding = true;
            result = Visit(expression);
        }

        if (result == null)
            throw new InvalidOperationException("Failed to translate Select projection.");

        if (result == QueryCompilationContext.NotTranslatedExpression)
            throw new InvalidOperationException(
                "Failed to translate Select projection to client evaluation.");

        if (!_indexBasedBinding)
            _selectExpression.ReplaceProjectionMapping(_projectionMapping);

        _projectionMapping.Clear();
        _selectExpression = null!;
        _projectionMembers.Clear();
        _indexBasedBinding = false;

        return result;
    }

    /// <summary>Handles anonymous type projections: new { x.Id, x.Name }</summary>
    protected override Expression VisitNew(NewExpression node)
    {
        // Parameterless constructors (e.g., new object())
        if (node.Arguments.Count == 0)
            return node;

        // Anonymous types must have members, but constructor DTO projections (new Dto(x.Prop, ...))
        // arrive without members. For these, use index-based client projection.
        var members = node.Members ?? TryInferMembers(node);

        switch (members)
        {
            case null when !_indexBasedBinding:
                return QueryCompilationContext.NotTranslatedExpression;
            case null:
            {
                var constructorArguments = new Expression[node.Arguments.Count];
                for (var i = 0; i < constructorArguments.Length; i++)
                {
                    var visitedArgument = Visit(node.Arguments[i]);
                    if (visitedArgument == QueryCompilationContext.NotTranslatedExpression)
                        return visitedArgument;
                    if (visitedArgument == null)
                        return QueryCompilationContext.NotTranslatedExpression;
                    constructorArguments[i] = visitedArgument;
                }

                return node.Update(constructorArguments);
            }
        }

        var newArguments = new Expression[node.Arguments.Count];
        for (var i = 0; i < newArguments.Length; i++)
        {
            var argument = node.Arguments[i];

            // Push member onto stack for hierarchical tracking
            var projectionMember = _projectionMembers.Peek().Append(members[i]);
            _projectionMembers.Push(projectionMember);

            var visitedArgument = Visit(argument);

            // Check if translation failed
            if (visitedArgument == QueryCompilationContext.NotTranslatedExpression)
                return visitedArgument;
            if (visitedArgument == null)
                return QueryCompilationContext.NotTranslatedExpression;

            _projectionMembers.Pop();

            newArguments[i] = visitedArgument;
        }

        // For inferred constructor projections, we must preserve the inferred member list.
        return node.Members == null
            ? Expression.New(node.Constructor!, newArguments, members)
            : node.Update(newArguments);
    }

    private static ReadOnlyCollection<MemberInfo>? TryInferMembers(NewExpression node)
    {
        if (node.Constructor == null)
            return null;

        var parameters = node.Constructor.GetParameters();
        if (parameters.Length != node.Arguments.Count)
            return null;

        var members = new MemberInfo[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameterName = parameters[i].Name;
            if (string.IsNullOrWhiteSpace(parameterName))
                return null;

            var property = node.Type.GetProperty(
                parameterName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

            if (property != null)
            {
                members[i] = property;
                continue;
            }

            var field = node.Type.GetField(
                parameterName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

            if (field == null)
                return null;

            members[i] = field;
        }

        return new ReadOnlyCollection<MemberInfo>(members);
    }

    /// <summary>Handles DTO projections: new ItemDto { Id = x.Id, Name = x.Name }</summary>
    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
        var newExpression = Visit(node.NewExpression);
        if (newExpression == QueryCompilationContext.NotTranslatedExpression)
            return newExpression;
        if (newExpression == null)
            return QueryCompilationContext.NotTranslatedExpression;

        var newBindings = new MemberBinding[node.Bindings.Count];
        for (var i = 0; i < newBindings.Length; i++)
        {
            if (node.Bindings[i].BindingType != MemberBindingType.Assignment)
                return QueryCompilationContext.NotTranslatedExpression;

            var memberAssignment = (MemberAssignment)node.Bindings[i];

            // Push member onto stack
            var projectionMember = _projectionMembers.Peek().Append(memberAssignment.Member);
            _projectionMembers.Push(projectionMember);

            var visitedExpression = Visit(memberAssignment.Expression);

            if (visitedExpression == QueryCompilationContext.NotTranslatedExpression)
                return visitedExpression;
            if (visitedExpression == null)
                return QueryCompilationContext.NotTranslatedExpression;

            _projectionMembers.Pop();

            newBindings[i] = memberAssignment.Update(visitedExpression);
        }

        return node.Update((NewExpression)newExpression, newBindings);
    }

    /// <summary>Handles StructuralTypeShaperExpression (entity shapers from previous query stage).</summary>
    protected override Expression VisitExtension(Expression node)
    {
        if (node is QueryParameterExpression)
            return node;

        if (node is MaterializeCollectionNavigationExpression
            materializeCollectionNavigationExpression)
        {
            if (!_indexBasedBinding)
                return QueryCompilationContext.NotTranslatedExpression;

            return base.VisitExtension(materializeCollectionNavigationExpression);
        }

        if (node is IncludeExpression includeExpression)
        {
            if (!_indexBasedBinding)
                return QueryCompilationContext.NotTranslatedExpression;

            return base.VisitExtension(includeExpression);
        }

        if (node is not StructuralTypeShaperExpression entityShaperExpression)
            return base.VisitExtension(node);

        var shaperEntityType = entityShaperExpression.StructuralType as IEntityType;
        var isOwnedShaper = shaperEntityType?.IsOwned() == true;

        if (isOwnedShaper)
        {
            if (!_indexBasedBinding)
                return QueryCompilationContext.NotTranslatedExpression;

            return entityShaperExpression;
        }

        if (_indexBasedBinding)
        {
            var indexEntityType = entityShaperExpression.StructuralType as IEntityType;
            if (indexEntityType == null)
                throw new InvalidOperationException(
                    $"Expected IEntityType but got {entityShaperExpression.StructuralType.GetType().Name}");

            var indexEntityProjection =
                new DynamoEntityProjectionExpression(indexEntityType, sqlExpressionFactory);

            foreach (var property in indexEntityType.GetProperties())
            {
                var sqlProperty = indexEntityProjection.BindProperty(property);
                _selectExpression.AddToProjection(sqlProperty, property.Name);
            }

            return entityShaperExpression;
        }

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

    /// <summary>Handles property access expressions (e.g., item.Pk, item.Name).</summary>
    protected override Expression VisitMember(MemberExpression node)
    {
        // If it's accessing a property on an entity shaper, translate to SQL property
        // Do NOT visit the inner expression to avoid expanding all entity properties
        if (node.Expression is StructuralTypeShaperExpression shaperExpression)
        {
            var shaperEntityType = shaperExpression.StructuralType as IEntityType;
            if (shaperEntityType?.IsOwned() == true)
            {
                if (!_indexBasedBinding)
                    return QueryCompilationContext.NotTranslatedExpression;

                var ownedInstance = Visit(node.Expression);
                if (ownedInstance == QueryCompilationContext.NotTranslatedExpression
                    || ownedInstance == null)
                    return QueryCompilationContext.NotTranslatedExpression;

                var ownedAccess = node.Update(ownedInstance);
                if (ownedInstance.Type.IsValueType)
                    return ownedAccess;

                return Expression.Condition(
                    Expression.Equal(ownedInstance, Expression.Constant(null, ownedInstance.Type)),
                    Expression.Default(node.Type),
                    ownedAccess);
            }

            var propertyName = node.Member.Name;
            var sqlProperty = sqlExpressionFactory.Property(propertyName, node.Type);

            if (_indexBasedBinding)
            {
                var index = _selectExpression.AddToProjection(sqlProperty, propertyName);
                return new ProjectionBindingExpression(_selectExpression, index, node.Type);
            }

            // Store mapping: ProjectionMember → SqlExpression
            _projectionMapping[_projectionMembers.Peek()] = sqlProperty;

            // Return ProjectionBindingExpression as placeholder
            return new ProjectionBindingExpression(
                _selectExpression,
                _projectionMembers.Peek(),
                node.Type);
        }

        if (!_indexBasedBinding)
        {
            // For other member accesses, try SQL translation
            var translation = sqlTranslator.Translate(node);
            if (translation is not SqlPropertyExpression)
                return QueryCompilationContext.NotTranslatedExpression;

            _projectionMapping[_projectionMembers.Peek()] = translation;

            return new ProjectionBindingExpression(
                _selectExpression,
                _projectionMembers.Peek(),
                node.Type);
        }

        if (node.Expression == null)
            return node;

        var instance = Visit(node.Expression);
        if (instance == QueryCompilationContext.NotTranslatedExpression)
            return instance;
        if (instance == null)
            return QueryCompilationContext.NotTranslatedExpression;

        var memberAccess = node.Update(instance);
        if (!instance.Type.IsValueType && IsOwnedNavigationAccess(node.Expression))
            return Expression.Condition(
                Expression.Equal(instance, Expression.Constant(null, instance.Type)),
                Expression.Default(node.Type),
                memberAccess);

        return memberAccess;
    }

    /// <summary>Determines whether a member-access chain originates from an owned navigation.</summary>
    private static bool IsOwnedNavigationAccess(Expression expression)
    {
        if (expression is not MemberExpression memberExpression)
            return false;

        if (memberExpression.Expression is StructuralTypeShaperExpression shaperExpression
            && shaperExpression.StructuralType is IEntityType entityType)
            return entityType
                    .FindNavigation(memberExpression.Member.Name)
                    ?.TargetEntityType
                    .IsOwned()
                == true;

        return memberExpression.Expression != null
            && IsOwnedNavigationAccess(memberExpression.Expression);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(EF)
            && node.Method.Name == nameof(EF.Property)
            && node.Arguments.Count == 2
            && node.Arguments[1] is ConstantExpression { Value: string propertyName })
        {
            if (node.Arguments[0] is StructuralTypeShaperExpression shaperExpression
                && shaperExpression.StructuralType is IEntityType entityType
                && entityType.IsOwned())
            {
                if (!_indexBasedBinding)
                    return QueryCompilationContext.NotTranslatedExpression;

                var ownedInstance = Visit(node.Arguments[0]);
                if (ownedInstance == QueryCompilationContext.NotTranslatedExpression
                    || ownedInstance == null)
                    return QueryCompilationContext.NotTranslatedExpression;

                var ownedMember =
                    ownedInstance.Type.GetProperty(propertyName) is not null
                        ?
                        Expression.Property(ownedInstance, propertyName)
                        : ownedInstance.Type.GetField(propertyName) is not null
                            ? Expression.Field(ownedInstance, propertyName)
                            : QueryCompilationContext.NotTranslatedExpression;

                if (ownedMember == QueryCompilationContext.NotTranslatedExpression)
                    return QueryCompilationContext.NotTranslatedExpression;

                var ownedValue = ownedMember;
                if (!ownedInstance.Type.IsValueType)
                    ownedValue = Expression.Condition(
                        Expression.Equal(
                            ownedInstance,
                            Expression.Constant(null, ownedInstance.Type)),
                        Expression.Default(ownedMember.Type),
                        ownedMember);

                return ownedValue.Type != node.Type
                    ? Expression.Convert(ownedValue, node.Type)
                    : ownedValue;
            }

            var sqlProperty = sqlExpressionFactory.Property(propertyName, node.Type);

            if (_indexBasedBinding)
            {
                var index = _selectExpression.AddToProjection(sqlProperty, propertyName);
                return new ProjectionBindingExpression(_selectExpression, index, node.Type);
            }

            _projectionMapping[_projectionMembers.Peek()] = sqlProperty;
            return new ProjectionBindingExpression(
                _selectExpression,
                _projectionMembers.Peek(),
                node.Type);
        }

        if (!_indexBasedBinding)
            return QueryCompilationContext.NotTranslatedExpression;

        var instance = node.Object == null ? null : Visit(node.Object);
        if (instance == QueryCompilationContext.NotTranslatedExpression)
            return instance;

        var arguments = new Expression[node.Arguments.Count];
        for (var i = 0; i < arguments.Length; i++)
        {
            var visitedArgument = Visit(node.Arguments[i]);
            if (visitedArgument == QueryCompilationContext.NotTranslatedExpression)
                return visitedArgument;
            if (visitedArgument == null)
                return QueryCompilationContext.NotTranslatedExpression;
            arguments[i] = visitedArgument;
        }

        return node.Update(instance, arguments);
    }

    /// <summary>Builds client-side computed projections (e.g. arithmetic, concat).</summary>
    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (!_indexBasedBinding)
            return QueryCompilationContext.NotTranslatedExpression;

        var left = Visit(node.Left);
        if (left == QueryCompilationContext.NotTranslatedExpression || left == null)
            return QueryCompilationContext.NotTranslatedExpression;

        var right = Visit(node.Right);
        if (right == QueryCompilationContext.NotTranslatedExpression || right == null)
            return QueryCompilationContext.NotTranslatedExpression;

        LambdaExpression? conversion = null;
        if (node.Conversion != null)
        {
            var visitedConversion = Visit(node.Conversion);
            if (visitedConversion == QueryCompilationContext.NotTranslatedExpression
                || visitedConversion == null)
                return QueryCompilationContext.NotTranslatedExpression;

            conversion = (LambdaExpression)visitedConversion;
        }

        return node.Update(left, conversion, right);
    }

    /// <summary>Builds client-side unary projections (e.g. conversions).</summary>
    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (!_indexBasedBinding)
            return QueryCompilationContext.NotTranslatedExpression;

        var operand = Visit(node.Operand);
        if (operand == QueryCompilationContext.NotTranslatedExpression || operand == null)
            return QueryCompilationContext.NotTranslatedExpression;

        return node.Update(operand);
    }

    /// <summary>Builds client-side conditional projections.</summary>
    protected override Expression VisitConditional(ConditionalExpression node)
    {
        if (!_indexBasedBinding)
            return QueryCompilationContext.NotTranslatedExpression;

        var test = Visit(node.Test);
        if (test == QueryCompilationContext.NotTranslatedExpression || test == null)
            return QueryCompilationContext.NotTranslatedExpression;

        var ifTrue = Visit(node.IfTrue);
        if (ifTrue == QueryCompilationContext.NotTranslatedExpression || ifTrue == null)
            return QueryCompilationContext.NotTranslatedExpression;

        var ifFalse = Visit(node.IfFalse);
        if (ifFalse == QueryCompilationContext.NotTranslatedExpression || ifFalse == null)
            return QueryCompilationContext.NotTranslatedExpression;

        return node.Update(test, ifTrue, ifFalse);
    }

    /// <summary>Default visitor for all other expressions - attempts SQL translation.</summary>
    public override Expression? Visit(Expression? node)
    {
        if (node == null)
            return null;

        if (_indexBasedBinding)
            return base.Visit(node);

        // Let specific visitor methods handle these
        if (node is NewExpression
            or MemberInitExpression
            or StructuralTypeShaperExpression
            or MemberExpression)
            return base.Visit(node);

        // Try to translate to SQL
        var translation = sqlTranslator.Translate(node);

        if (translation is not SqlPropertyExpression)
            return QueryCompilationContext.NotTranslatedExpression;

        // Store mapping: ProjectionMember → SqlExpression
        _projectionMapping[_projectionMembers.Peek()] = translation;

        // Return ProjectionBindingExpression as placeholder
        return new ProjectionBindingExpression(
            _selectExpression,
            _projectionMembers.Peek(),
            node.Type);
    }

}
