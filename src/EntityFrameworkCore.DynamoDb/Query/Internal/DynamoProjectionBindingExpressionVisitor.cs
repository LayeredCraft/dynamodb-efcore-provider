using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

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

    private bool _indexBasedBinding;
    private SelectExpression _selectExpression = null!;

    /// <summary>Translates a Select lambda body into a projection mapping and returns a shaper expression.</summary>
    public Expression Translate(SelectExpression selectExpression, Expression expression)
    {
        try
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

            return result;
        }
        finally
        {
            _projectionMapping.Clear();
            _selectExpression = null!;
            _projectionMembers.Clear();
            _indexBasedBinding = false;
        }
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

    /// <summary>Handles StructuralTypeShaperExpression (entity and complex type shapers from previous query stage).</summary>
    protected override Expression VisitExtension(Expression node)
    {
        if (node is QueryParameterExpression)
            return node;

        // Complex type shapers pass through to base; EF Core handles structural materialization
        if (node is StructuralTypeShaperExpression { StructuralType: IComplexType })
            return base.VisitExtension(node);

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

        var entityType = entityShaperExpression.StructuralType as IEntityType;
        if (entityType == null)
            throw new InvalidOperationException(
                $"Expected IEntityType but got {entityShaperExpression.StructuralType.GetType().Name}");

        if (_indexBasedBinding)
        {
            var entityProjection =
                new DynamoEntityProjectionExpression(entityType, sqlExpressionFactory);

            foreach (var property in GetProjectedProperties(entityType))
            {
                if (property.IsRuntimeOnly())
                    continue;
                var sqlProperty = entityProjection.BindProperty(property);
                _selectExpression.AddToProjection(sqlProperty, property.Name);
            }

            // Add complex property map attributes so they are fetched in the projection
            foreach (var complexProperty in entityType.GetComplexProperties())
            {
                var attributeName = ((IReadOnlyComplexProperty)complexProperty).GetAttributeName();
                _selectExpression.AddEmbeddedAttributeToProjection(attributeName);
            }

            return entityShaperExpression;
        }

        var projectionBindingExpression =
            (ProjectionBindingExpression)entityShaperExpression.ValueBufferExpression;

        if (projectionBindingExpression.QueryExpression != _selectExpression)
            throw new InvalidOperationException(
                "ProjectionBindingExpression belongs to a different SelectExpression.");

        if (projectionBindingExpression.ProjectionMember == null)
            throw new InvalidOperationException(
                "ProjectionBindingExpression has null ProjectionMember.");

        var mappedProjection =
            _selectExpression.GetMappedProjection(projectionBindingExpression.ProjectionMember);

        if (mappedProjection is not DynamoEntityProjectionExpression dynamoEntityProjection)
            throw new InvalidOperationException(
                $"Expected DynamoEntityProjectionExpression but got {mappedProjection.GetType().Name}");

        foreach (var property in GetProjectedProperties(entityType))
        {
            if (property.IsRuntimeOnly())
                continue;
            var sqlProperty = dynamoEntityProjection.BindProperty(property);
            var memberInfo = DynamoEntityProjectionExpression.GetMemberInfo(property);
            var projectionMember = _projectionMembers.Peek().Append(memberInfo);
            _projectionMapping[projectionMember] = sqlProperty;
        }

        // Add complex property map attributes so they are fetched in the projection
        foreach (var complexProperty in entityType.GetComplexProperties())
        {
            var attributeName = ((IReadOnlyComplexProperty)complexProperty).GetAttributeName();
            _selectExpression.AddEmbeddedAttributeToProjection(attributeName);
        }

        return new ProjectionBindingExpression(
            _selectExpression,
            _projectionMembers.Peek(),
            typeof(ValueBuffer));
    }

    /// <summary>Handles property access expressions (e.g., item.Pk, item.Name).</summary>
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is StructuralTypeShaperExpression shaperExpression)
        {
            var shaperEntityType = shaperExpression.StructuralType as IEntityType;
            var propertyName = node.Member.Name;

            // Complex property access (e.g. Select(e => e.Profile)): delegate to context-aware
            // complex type projection so the removing visitor can push the correct map context.
            var complexProperty = shaperExpression.StructuralType.FindComplexProperty(propertyName);
            if (complexProperty != null)
            {
                if (!_indexBasedBinding)
                    return QueryCompilationContext.NotTranslatedExpression;

                var attributeName = ((IReadOnlyComplexProperty)complexProperty).GetAttributeName();
                _selectExpression.AddEmbeddedAttributeToProjection(attributeName);

                if (complexProperty.IsCollection)
                {
                    var elementShaper = new StructuralTypeShaperExpression(
                        complexProperty.ComplexType,
                        Expression.Constant(ValueBuffer.Empty),
                        false);

                    return new DynamoComplexCollectionProjectionExpression(
                        complexProperty,
                        elementShaper);
                }

                var innerShaper = new StructuralTypeShaperExpression(
                    complexProperty.ComplexType,
                    Expression.Constant(ValueBuffer.Empty),
                    complexProperty.IsNullable);

                return new DynamoComplexTypeProjectionExpression(complexProperty, innerShaper);
            }

            var sqlProperty =
                ResolveScalarPropertyExpression(shaperEntityType, propertyName)
                ?? sqlExpressionFactory.Property(propertyName, node.Type);

            if (_indexBasedBinding)
            {
                var index = _selectExpression.AddToProjection(sqlProperty, propertyName);
                var projectionBinding = new ProjectionBindingExpression(
                    _selectExpression,
                    index,
                    sqlProperty.Type);

                return projectionBinding.Type == node.Type
                    ? projectionBinding
                    : Expression.Convert(projectionBinding, node.Type);
            }

            _projectionMapping[_projectionMembers.Peek()] = sqlProperty;

            var memberProjectionBinding = new ProjectionBindingExpression(
                _selectExpression,
                _projectionMembers.Peek(),
                sqlProperty.Type);

            return memberProjectionBinding.Type == node.Type
                ? memberProjectionBinding
                : Expression.Convert(memberProjectionBinding, node.Type);
        }

        if (!_indexBasedBinding)
        {
            // For other member accesses, try SQL translation.
            // DynamoScalarAccessExpression (nested path) is not supported in SELECT projections —
            // nested paths are WHERE-only. Return NotTranslated so EF Core falls back to
            // client-side evaluation via the index-based binding pass.
            var translation = sqlTranslator.Translate(node);
            if (translation is null or DynamoScalarAccessExpression)
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

        return node.Update(instance);
    }

    /// <summary>Handles EF.Property&lt;T&gt; calls and general method call expressions.</summary>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(EF)
            && node.Method.Name == nameof(EF.Property)
            && node.Arguments.Count == 2
            && node.Arguments[1] is ConstantExpression { Value: string propertyName })
        {
            var ownerStructuralType =
                (node.Arguments[0] as StructuralTypeShaperExpression)?.StructuralType
                ?? (ITypeBase?)model.FindEntityType(node.Arguments[0].Type);

            // Complex property via EF.Property<T>(e, "Profile"): same context-push logic as
            // VisitMember.
            var complexProperty = ownerStructuralType?.FindComplexProperty(propertyName);
            if (complexProperty != null)
            {
                if (!_indexBasedBinding)
                    return QueryCompilationContext.NotTranslatedExpression;

                var attributeName = ((IReadOnlyComplexProperty)complexProperty).GetAttributeName();
                _selectExpression.AddEmbeddedAttributeToProjection(attributeName);

                if (complexProperty.IsCollection)
                {
                    var elementShaper = new StructuralTypeShaperExpression(
                        complexProperty.ComplexType,
                        Expression.Constant(ValueBuffer.Empty),
                        false);

                    return new DynamoComplexCollectionProjectionExpression(
                        complexProperty,
                        elementShaper);
                }

                var innerShaper = new StructuralTypeShaperExpression(
                    complexProperty.ComplexType,
                    Expression.Constant(ValueBuffer.Empty),
                    complexProperty.IsNullable);

                return new DynamoComplexTypeProjectionExpression(complexProperty, innerShaper);
            }

            var ownerEntityType = ownerStructuralType as IEntityType;

            var sqlProperty =
                ResolveScalarPropertyExpression(ownerEntityType, propertyName)
                ?? sqlExpressionFactory.Property(propertyName, node.Type);

            if (_indexBasedBinding)
            {
                var index = _selectExpression.AddToProjection(sqlProperty, propertyName);
                var projectionBinding = new ProjectionBindingExpression(
                    _selectExpression,
                    index,
                    sqlProperty.Type);

                return projectionBinding.Type == node.Type
                    ? projectionBinding
                    : Expression.Convert(projectionBinding, node.Type);
            }

            _projectionMapping[_projectionMembers.Peek()] = sqlProperty;
            var methodProjectionBinding = new ProjectionBindingExpression(
                _selectExpression,
                _projectionMembers.Peek(),
                sqlProperty.Type);

            return methodProjectionBinding.Type == node.Type
                ? methodProjectionBinding
                : Expression.Convert(methodProjectionBinding, node.Type);
        }

        if (!_indexBasedBinding)
            return QueryCompilationContext.NotTranslatedExpression;

        var objInstance = node.Object == null ? null : Visit(node.Object);
        if (objInstance == QueryCompilationContext.NotTranslatedExpression)
            return objInstance;

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

        return node.Update(objInstance, arguments);
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

    /// <summary>
    ///     Resolves a scalar projection to a metadata-backed SQL property expression when the member
    ///     maps to an EF property.
    /// </summary>
    /// <remarks>
    ///     Uses <c>DynamoPropertyExtensions.GetAttributeName</c> so scalar projections honor
    ///     <c>HasAttributeName</c>, and applies the property type mapping so converter-backed projections
    ///     materialize with the correct wire conversion.
    /// </remarks>
    private SqlPropertyExpression? ResolveScalarPropertyExpression(
        IEntityType? entityType,
        string propertyName)
    {
        var property = entityType?.FindProperty(propertyName);
        if (property == null)
            return null;

        var sqlProperty =
            sqlExpressionFactory.Property(property.GetAttributeName(), property.ClrType);

        return sqlProperty.ApplyTypeMapping(property.GetTypeMapping());
    }

    /// <summary>
    ///     Gets properties that should be projected for entity materialization, including
    ///     base-type and descendant declared properties required for shared-table inheritance
    ///     materialization.
    /// </summary>
    private static IEnumerable<IProperty> GetProjectedProperties(IEntityType entityType)
        => GetProjectedEntityTypes(entityType).SelectMany(static t => t.GetDeclaredProperties());

    /// <summary>
    ///     Gets the hierarchy entity types whose declared members must be projected for the current
    ///     query shape.
    /// </summary>
    /// <remarks>
    ///     For root/shared-table queries we include each concrete descendant so discriminator-based
    ///     materialization can bind derived properties. For derived-type queries we include the
    ///     queried type, its base chain, and only its own descendants (not siblings).
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
}
