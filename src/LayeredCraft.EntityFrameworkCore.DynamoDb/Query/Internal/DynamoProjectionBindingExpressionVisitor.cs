using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
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
    ISqlExpressionFactory sqlExpressionFactory,
    IModel model) : ExpressionVisitor
{
    private readonly Dictionary<ProjectionMember, Expression> _projectionMapping = new();
    private readonly Stack<ProjectionMember> _projectionMembers = new();

    private readonly Dictionary<IEntityType, HashSet<string>> _topLevelOwnedContainerNameCache =
        new();

    private readonly Dictionary<IEntityType, HashSet<string>>
        _nestedOwnedContainerNameCache = new();

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

            if (materializeCollectionNavigationExpression.Navigation is INavigation
                    embeddedNavigation
                && embeddedNavigation.IsEmbedded())
            {
                // Embedded collections are projected as their containing DynamoDB list attribute.
                // We retain the best available element shaper so downstream materialization can
                // still populate owned members and shadow state consistently.
                var elementShaperExpression =
                    TryExtractCollectionNavigationElementShaperExpression(
                        materializeCollectionNavigationExpression.Subquery,
                        embeddedNavigation)
                    ?? TryExtractElementShaperExpression(
                        materializeCollectionNavigationExpression.Subquery,
                        embeddedNavigation.TargetEntityType);
                elementShaperExpression ??= new StructuralTypeShaperExpression(
                    embeddedNavigation.TargetEntityType,
                    Expression.Constant(ValueBuffer.Empty),
                    true);

                var elementType =
                    DynamoTypeMappingSource.TryGetListElementType(
                        embeddedNavigation.ClrType,
                        out var resolvedElementType)
                        ? resolvedElementType
                        : embeddedNavigation.TargetEntityType.ClrType;

                var attributeName =
                    embeddedNavigation.TargetEntityType.GetContainingAttributeName()
                    ?? embeddedNavigation.Name;

                _selectExpression.AddEmbeddedAttributeToProjection(attributeName);

                return new DynamoCollectionShaperExpression(
                    new DynamoObjectArrayProjectionExpression(embeddedNavigation, attributeName),
                    elementShaperExpression,
                    embeddedNavigation,
                    elementType);
            }

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
            var topLevelOwnedContainingAttributeNames =
                GetTopLevelOwnedContainingAttributeNames(indexEntityType);

            foreach (var property in indexEntityType.GetProperties())
            {
                if (!OwnedProjectionMetadata.ShouldProjectTopLevelProperty(
                    indexEntityType,
                    property,
                    topLevelOwnedContainingAttributeNames))
                    continue;

                var sqlProperty = indexEntityProjection.BindProperty(property);
                _selectExpression.AddToProjection(sqlProperty, property.Name);
            }

            foreach (var containingAttributeName in topLevelOwnedContainingAttributeNames)
                _selectExpression.AddEmbeddedAttributeToProjection(containingAttributeName);

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
        var mappingTopLevelOwnedContainingAttributeNames =
            GetTopLevelOwnedContainingAttributeNames(entityType);
        foreach (var property in entityType.GetProperties())
        {
            if (!OwnedProjectionMetadata.ShouldProjectTopLevelProperty(
                entityType,
                property,
                mappingTopLevelOwnedContainingAttributeNames))
                continue;

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
            if (shaperEntityType != null
                && IsNestedOwnedContainingAttributeName(shaperEntityType, propertyName))
                throw new InvalidOperationException(
                    $"Cannot project nested owned container '{propertyName}' from entity '{shaperEntityType.DisplayName()}' as a top-level attribute. "
                    + "Project the top-level owned container and access nested members via dot notation.");

            // Detect owned embedded reference navigations and preserve INavigation so the
            // projection-binding removal visitor can materialise them without a model-wide scan.
            if (shaperEntityType != null)
            {
                var nav = shaperEntityType.FindNavigation(propertyName);
                if (nav != null
                    && !nav.IsCollection
                    && nav.IsEmbedded()
                    && nav.TargetEntityType.IsOwned())
                {
                    var objectAccess = new DynamoObjectAccessExpression(nav);
                    if (_indexBasedBinding)
                    {
                        var idx = _selectExpression.AddToProjection(objectAccess, propertyName);
                        return new ProjectionBindingExpression(_selectExpression, idx, node.Type);
                    }

                    _projectionMapping[_projectionMembers.Peek()] = objectAccess;
                    return new ProjectionBindingExpression(
                        _selectExpression,
                        _projectionMembers.Peek(),
                        node.Type);
                }
            }

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
            var ownerEntityType =
                (node.Arguments[0] as StructuralTypeShaperExpression)
                ?.StructuralType as IEntityType;
            ownerEntityType ??= model.FindEntityType(node.Arguments[0].Type);

            if (ownerEntityType is { } ownedEntityType && ownedEntityType.IsOwned())
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

            // When NEEV reduces OwnedNavigationReference → EF.Property(shaper, "Profile"),
            // preserve INavigation so the removing visitor can materialise unambiguously.
            if (ownerEntityType != null)
            {
                var nav = ownerEntityType.FindNavigation(propertyName);
                if (nav != null
                    && !nav.IsCollection
                    && nav.IsEmbedded()
                    && nav.TargetEntityType.IsOwned())
                {
                    var objectAccess = new DynamoObjectAccessExpression(nav);
                    if (_indexBasedBinding)
                    {
                        var idx = _selectExpression.AddToProjection(objectAccess, propertyName);
                        return new ProjectionBindingExpression(_selectExpression, idx, node.Type);
                    }

                    _projectionMapping[_projectionMembers.Peek()] = objectAccess;
                    return new ProjectionBindingExpression(
                        _selectExpression,
                        _projectionMembers.Peek(),
                        node.Type);
                }
            }

            var sqlProperty = sqlExpressionFactory.Property(propertyName, node.Type);
            if (ownerEntityType != null
                && IsNestedOwnedContainingAttributeName(ownerEntityType, propertyName))
                throw new InvalidOperationException(
                    $"Cannot project nested owned container '{propertyName}' from entity '{ownerEntityType.DisplayName()}' as a top-level attribute. "
                    + "Project the top-level owned container and access nested members via dot notation.");

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

    /// <summary>Gets top-level owned containing-attribute names for a root entity, using a local cache.</summary>
    private HashSet<string> GetTopLevelOwnedContainingAttributeNames(IEntityType entityType)
    {
        if (_topLevelOwnedContainerNameCache.TryGetValue(entityType, out var cached))
            return cached;

        var topLevelNames =
            OwnedProjectionMetadata.GetTopLevelOwnedContainingAttributeNames(entityType);

        _topLevelOwnedContainerNameCache[entityType] = topLevelNames;
        return topLevelNames;
    }

    /// <summary>Gets nested owned containing-attribute names for a root entity, using a local cache.</summary>
    private HashSet<string> GetNestedOwnedContainingAttributeNames(IEntityType entityType)
    {
        if (_nestedOwnedContainerNameCache.TryGetValue(entityType, out var cached))
            return cached;

        var nestedNames =
            OwnedProjectionMetadata.GetNestedOwnedContainingAttributeNames(entityType);

        _nestedOwnedContainerNameCache[entityType] = nestedNames;
        return nestedNames;
    }

    /// <summary>Determines whether a property name is a nested owned containing attribute for an entity.</summary>
    private bool IsNestedOwnedContainingAttributeName(IEntityType entityType, string propertyName)
    {
        var nestedOwnedContainingAttributeNames =
            GetNestedOwnedContainingAttributeNames(entityType);
        return nestedOwnedContainingAttributeNames.Contains(propertyName);
    }

    /// <summary>Finds an owned-collection element shaper expression within an expression tree.</summary>
    private static Expression? TryExtractElementShaperExpression(
        Expression expression,
        IEntityType targetEntityType)
    {
        if (expression is ShapedQueryExpression shapedQueryExpression)
            return TryExtractElementShaperExpression(
                shapedQueryExpression.ShaperExpression,
                targetEntityType);

        if (IsTargetShaper(expression, targetEntityType))
            return expression;

        var finder = new ElementShaperExpressionFinder(targetEntityType);
        finder.Visit(expression);
        return finder.Result;
    }

    /// <summary>Finds an owned collection element shaper expression for a specific collection navigation.</summary>
    private static Expression? TryExtractCollectionNavigationElementShaperExpression(
        Expression expression,
        INavigation navigation)
    {
        var finder = new NavigationCollectionShaperFinder(navigation);
        finder.Visit(expression);
        return finder.Result;
    }

    /// <summary>
    ///     Determines whether an expression represents a structural shaper for the target entity
    ///     type.
    /// </summary>
    private static bool IsTargetShaper(Expression expression, IEntityType targetEntityType)
        => expression is StructuralTypeShaperExpression
            {
                StructuralType: IEntityType { ClrType: var clrType },
            }
            && clrType == targetEntityType.ClrType;

    /// <summary>Traverses an expression tree to locate a nested collection element shaper.</summary>
    /// <remarks>
    ///     A copy of this class also lives in
    ///     <see cref="DynamoProjectionBindingRemovingExpressionVisitor" />; the duplication is deliberate
    ///     to avoid cross-visitor coupling.
    /// </remarks>
    private sealed class ElementShaperExpressionFinder : ExpressionVisitor
    {
        private readonly IEntityType _targetEntityType;

        /// <summary>Creates a finder scoped to a specific target entity type.</summary>
        public ElementShaperExpressionFinder(IEntityType targetEntityType)
            => _targetEntityType = targetEntityType;

        /// <summary>Gets the first element shaper expression found during traversal.</summary>
        public Expression? Result { get; private set; }

        /// <summary>Visits expressions until a shaped query expression is discovered.</summary>
        public override Expression? Visit(Expression? node)
        {
            if (node == null || Result != null)
                return node;

            if (node is ShapedQueryExpression shapedQueryExpression)
                return Visit(shapedQueryExpression.ShaperExpression);

            if (node is StructuralTypeShaperExpression
                {
                    StructuralType: IEntityType { ClrType: var clrType },
                } structuralTypeShaperExpression
                && clrType == _targetEntityType.ClrType)
            {
                Result = structuralTypeShaperExpression;
                return node;
            }

            return base.Visit(node);
        }
    }

    /// <summary>Finds whether a target structural shaper exists within a tree.</summary>
    /// <remarks>
    ///     A copy of this class also lives in
    ///     <see cref="DynamoProjectionBindingRemovingExpressionVisitor" />; the duplication is deliberate
    ///     to avoid cross-visitor coupling.
    /// </remarks>
    private sealed class TargetShaperPresenceFinder(IEntityType targetEntityType)
        : ExpressionVisitor
    {
        /// <summary>Gets whether a target structural shaper has been found.</summary>
        public bool Found { get; private set; }

        /// <summary>Visits nodes until a matching structural shaper is found.</summary>
        public override Expression? Visit(Expression? node)
        {
            if (node == null || Found)
                return node;

            if (IsTargetShaper(node, targetEntityType))
            {
                Found = true;
                return node;
            }

            return base.Visit(node);
        }
    }

    /// <summary>Finds a nested collection element shaper for a specific collection navigation.</summary>
    private sealed class NavigationCollectionShaperFinder(INavigation navigation)
        : ExpressionVisitor
    {
        /// <summary>Gets the first matching element shaper expression found during traversal.</summary>
        public Expression? Result { get; private set; }

        /// <summary>Visits nodes until a matching collection-shaper expression is discovered.</summary>
        public override Expression? Visit(Expression? node)
        {
            if (node == null || Result != null)
                return node;

            // In the projection-binding phase the full ShapedQueryExpression.ShaperExpression is
            // returned. Callers (TryExtractCollectionNavigationElementShaperExpression) pass this
            // on to DynamoCollectionShaperExpression construction; the removing visitor receives it
            // as InnerShaper and drills deeper at that stage via its own finder.
            if (node is MaterializeCollectionNavigationExpression
                {
                    Navigation: INavigation candidateNavigation,
                    Subquery: ShapedQueryExpression { ShaperExpression: var shaperExpression },
                }
                && candidateNavigation.Name == navigation.Name
                && candidateNavigation.TargetEntityType.ClrType
                == navigation.TargetEntityType.ClrType)
            {
                Result = shaperExpression;
                return node;
            }

            if (node is DynamoCollectionShaperExpression
                {
                    Navigation: INavigation candidateDynamoNavigation,
                    InnerShaper: var dynamoShaperExpression,
                }
                && candidateDynamoNavigation.Name == navigation.Name
                && candidateDynamoNavigation.TargetEntityType.ClrType
                == navigation.TargetEntityType.ClrType)
            {
                Result = dynamoShaperExpression;
                return node;
            }

            return base.Visit(node);
        }
    }
}
