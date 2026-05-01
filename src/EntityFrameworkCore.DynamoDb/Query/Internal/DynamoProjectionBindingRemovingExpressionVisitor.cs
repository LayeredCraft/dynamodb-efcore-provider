using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using static System.Linq.Expressions.Expression;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Replaces EF Core's abstract ProjectionBindingExpression nodes with concrete expression
///     trees that extract property values from Dictionary&lt;string, AttributeValue&gt;.
/// </summary>
/// <remarks>
///     Builds expression trees at query compilation time, inlining all type conversions to
///     eliminate runtime boxing. The compiled query executes AttributeValue deserialization and EF
///     Core value conversions as pure IL with zero boxing overhead.
///     Complex type properties are handled via
///     <see cref="DynamoComplexPropertyInitializationExpression" /> and
///     <see cref="DynamoComplexCollectionInitializationExpression" /> markers emitted by
///     <see cref="DynamoShapedQueryCompilingExpressionVisitor.AddStructuralTypeInitialization" />.
/// </remarks>
public class DynamoProjectionBindingRemovingExpressionVisitor(
    ParameterExpression itemParameter,
    SelectExpression selectExpression) : ExpressionVisitor
{
    // Reflection cache for efficient expression tree construction
    private static readonly PropertyInfo AttributeValueSProperty =
        typeof(AttributeValue).GetProperty(nameof(AttributeValue.S))!;

    private static readonly PropertyInfo AttributeValueBoolProperty =
        typeof(AttributeValue).GetProperty(nameof(AttributeValue.BOOL))!;

    private static readonly PropertyInfo AttributeValueNProperty =
        typeof(AttributeValue).GetProperty(nameof(AttributeValue.N))!;

    private static readonly PropertyInfo AttributeValueBProperty =
        typeof(AttributeValue).GetProperty(nameof(AttributeValue.B))!;

    private static readonly PropertyInfo AttributeValueNullProperty =
        typeof(AttributeValue).GetProperty(nameof(AttributeValue.NULL))!;

    private static readonly PropertyInfo AttributeValueMProperty =
        typeof(AttributeValue).GetProperty(nameof(AttributeValue.M))!;

    private static readonly PropertyInfo AttributeValueLProperty =
        typeof(AttributeValue).GetProperty(nameof(AttributeValue.L))!;

    private static readonly PropertyInfo AttributeValueSsProperty =
        typeof(AttributeValue).GetProperty(nameof(AttributeValue.SS))!;

    private static readonly PropertyInfo AttributeValueNsProperty =
        typeof(AttributeValue).GetProperty(nameof(AttributeValue.NS))!;

    private static readonly PropertyInfo AttributeValueBsProperty =
        typeof(AttributeValue).GetProperty(nameof(AttributeValue.BS))!;

    private static readonly ConstructorInfo InvalidOperationExceptionCtor =
        typeof(InvalidOperationException).GetConstructor([typeof(string)])!;

    private static readonly MethodInfo DictionaryTryGetValueMethod =
        typeof(Dictionary<string, AttributeValue>).GetMethod(nameof(Dictionary<,>.TryGetValue))!;

    private static readonly ConstantExpression InvariantCultureExpression =
        Constant(CultureInfo.InvariantCulture, typeof(IFormatProvider));

    private static readonly ConstantExpression IntegerNumberStylesExpression =
        Constant(NumberStyles.Integer, typeof(NumberStyles));

    private static readonly ConstantExpression FloatNumberStylesExpression =
        Constant(NumberStyles.Float, typeof(NumberStyles));

    private static readonly ConcurrentDictionary<Type, MethodInfo> NumericParseMethodCache = new();

    private static readonly MethodInfo MemoryStreamToArrayMethod =
        typeof(MemoryStream).GetMethod(nameof(MemoryStream.ToArray))!;

    private static readonly IReadOnlyDictionary<string, Func<Expression, Expression>>
        RuntimeValueSourceFactories =
            new Dictionary<string, Func<Expression, Expression>>(StringComparer.Ordinal)
            {
                [DynamoRuntimeValueSources.CurrentPageResponse] = static queryContextParameter
                    => Property(
                        Convert(queryContextParameter, typeof(DynamoQueryContext)),
                        nameof(DynamoQueryContext.CurrentPageResponse)),
            };

    /// <summary>
    ///     Stack of the current dictionary context being deserialized. The bottom entry is the root
    ///     item dictionary; each pushed entry is a nested complex type or owned reference map.
    /// </summary>
    private readonly Stack<ParameterExpression> _attributeContextStack = new([itemParameter]);

    /// <summary>
    ///     Intercepts MaterializationContext constructor calls to replace ProjectionBindingExpression
    ///     with ValueBuffer.Empty placeholder (actual data comes from Dictionary access). For anonymous
    ///     types and DTOs, visits all arguments normally.
    /// </summary>
    protected override Expression VisitNew(NewExpression node)
    {
        // Check if this is a MaterializationContext construction (entity materialization)
        // by checking if the first argument type is ValueBuffer
        if (node.Arguments.Count > 0
            && node.Arguments[0] is ProjectionBindingExpression pbe
            && pbe.Type == typeof(ValueBuffer))
        {
            // new MaterializationContext(ValueBuffer.Empty, ...)
            List<Expression> newArguments = [Constant(ValueBuffer.Empty)];

            for (var i = 1; i < node.Arguments.Count; i++)
                newArguments.Add(Visit(node.Arguments[i]));

            return node.Update(newArguments);
        }

        // For anonymous types and DTOs, visit all arguments normally
        // (arguments will be ProjectionBindingExpression that get replaced with dictionary access)
        return base.VisitNew(node);
    }

    /// <summary>Rewrites init-only member assignments emitted during materializer injection.</summary>
    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (node.NodeType == ExpressionType.Assign)
        {
            if (node.Left is MemberExpression
                {
                    Member: FieldInfo { IsInitOnly: true },
                } memberExpression)
                return memberExpression.Assign(Visit(node.Right));

            var visitedRight = Visit(node.Right);
            return visitedRight == node.Right
                ? node
                : node.Update(node.Left, node.Conversion, visitedRight);
        }

        return base.VisitBinary(node);
    }

    /// <summary>
    ///     Rewrites nested member access to preserve null propagation when the containing complex
    ///     instance materializes as <see langword="null" />.
    /// </summary>
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression == null)
            return base.VisitMember(node);

        var instanceExpression = Visit(node.Expression);
        if (instanceExpression == QueryCompilationContext.NotTranslatedExpression
            || instanceExpression == null)
            return QueryCompilationContext.NotTranslatedExpression;

        var memberAccess = node.Update(instanceExpression);
        return ShouldApplyNullPropagation(instanceExpression.Type)
            ? ApplyNullPropagation(instanceExpression, memberAccess, node.Type)
            : memberAccess;
    }

    /// <summary>
    ///     Handles projection-binding and complex-type marker nodes. Converts member-based
    ///     bindings to indexed dictionary access and expands complex property markers with the
    ///     correct nested map context.
    /// </summary>
    protected override Expression VisitExtension(Expression node)
    {
        // Complex property initialization markers emitted by AddStructuralTypeInitialization.
        if (node is DynamoComplexPropertyInitializationExpression complexPropInit)
            return VisitComplexPropertyInitialization(complexPropInit);

        // Direct complex-type projection from Select(e => e.Profile) or Select(e => new { e.Profile
        // }).
        if (node is DynamoComplexTypeProjectionExpression complexTypeProjection)
            return VisitComplexTypeProjection(complexTypeProjection);

        if (node is DynamoComplexCollectionProjectionExpression complexCollectionProjection)
            return VisitComplexCollectionProjection(complexCollectionProjection);

        if (node is DynamoComplexCollectionInitializationExpression complexCollInit)
            return VisitComplexCollectionInitialization(complexCollInit);

        if (node is MaterializeCollectionNavigationExpression
            materializeCollectionNavigationExpression)
        {
            var navigation = materializeCollectionNavigationExpression.Navigation;

            var navigationExpression = CreateGetValueExpression(
                navigation.Name,
                navigation.ClrType,
                null,
                false,
                navigation.DeclaringEntityType.DisplayName(),
                null);

            return ConvertCollectionMaterialization(navigationExpression, navigation.ClrType);
        }

        if (node is IncludeExpression includeExpression)
        {
            var entityExpression = Visit(includeExpression.EntityExpression);
            if (entityExpression == QueryCompilationContext.NotTranslatedExpression
                || entityExpression == null)
                return QueryCompilationContext.NotTranslatedExpression;

            if (includeExpression.Navigation is not INavigation navigation
                || navigation.PropertyInfo is null
                || !navigation.PropertyInfo.CanWrite)
                return entityExpression;

            var entityVariable = Variable(entityExpression.Type, "includedEntity");
            var assignEntity = Assign(entityVariable, entityExpression);
            var visitedNavigationExpression = Visit(includeExpression.NavigationExpression);
            if (visitedNavigationExpression == QueryCompilationContext.NotTranslatedExpression
                || visitedNavigationExpression == null)
                return QueryCompilationContext.NotTranslatedExpression;

            var navigationAssignment = Assign(
                Property(entityVariable, navigation.PropertyInfo),
                ConvertCollectionMaterialization(visitedNavigationExpression, navigation.ClrType));

            var includeBody = Block(navigationAssignment, entityVariable);

            if (!entityExpression.Type.IsValueType)
                return Block(
                    [entityVariable],
                    assignEntity,
                    Condition(
                        Equal(entityVariable, Constant(null, entityExpression.Type)),
                        Constant(null, entityExpression.Type),
                        includeBody));

            return Block([entityVariable], assignEntity, includeBody);
        }

        if (node is ProjectionBindingExpression projectionBinding)
        {
            // After ApplyProjection(), mapping contains Constant(index)
            if (projectionBinding.ProjectionMember != null)
            {
                var indexConstant = (ConstantExpression)selectExpression.GetMappedProjection(
                    projectionBinding.ProjectionMember);
                var index = (int)indexConstant.Value!;

                var projection = selectExpression.Projection[index];

                var propertyName = projection.Expression is SqlPropertyExpression propertyExpression
                    ? propertyExpression.PropertyName
                    : projection.Alias;

                // Get type mapping from SQL expression for converter support
                var typeMapping = projection.Expression.TypeMapping;

                // For custom projections, we only have the CLR type, not IProperty metadata.
                // Enforce strict requiredness for non-nullable value types to align with
                // relational-style materialization semantics.
                var required = IsNonNullableValueType(projectionBinding.Type);

                return CreateGetValueExpression(
                    propertyName,
                    projectionBinding.Type,
                    typeMapping,
                    required,
                    null,
                    null);
            }

            if (projectionBinding.Index != null)
            {
                var index = projectionBinding.Index.Value;
                var projection = selectExpression.Projection[index];

                var propertyName = projection.Expression is SqlPropertyExpression propertyExpression
                    ? propertyExpression.PropertyName
                    : projection.Alias;

                var typeMapping = projection.Expression.TypeMapping;
                var required = IsNonNullableValueType(projectionBinding.Type);

                return CreateGetValueExpression(
                    propertyName,
                    projectionBinding.Type,
                    typeMapping,
                    required,
                    null,
                    null);
            }
        }

        if (ReferenceEquals(node, QueryCompilationContext.NotTranslatedExpression))
            return node;

        return base.VisitExtension(node);
    }

    /// <summary>
    ///     Visits complex property initialization markers by switching to the nested attribute map
    ///     context for the duration of the injected scalar materializer.
    /// </summary>
    private Expression VisitComplexPropertyInitialization(
        DynamoComplexPropertyInitializationExpression complexInit)
    {
        var cp = complexInit.ComplexProperty;
        var attributeName = ((IReadOnlyComplexProperty)cp).GetAttributeName();
        var required = !cp.IsNullable;
        var path = $"{cp.DeclaringType.DisplayName()}.{cp.Name}";

        var complexMapVariable = Variable(
            typeof(Dictionary<string, AttributeValue>),
            $"complex_{attributeName}_map");

        var readComplexMapExpression = CreateReadComplexMapExpression(
            _attributeContextStack.Peek(),
            attributeName,
            required,
            path);

        _attributeContextStack.Push(complexMapVariable);
        var visitedMaterializer = Visit(complexInit.InjectedMaterializer);
        _attributeContextStack.Pop();

        Expression complexInstance = cp.IsNullable
            ? Condition(
                Equal(
                    complexMapVariable,
                    Constant(null, typeof(Dictionary<string, AttributeValue>))),
                Constant(null, cp.ClrType),
                Convert(visitedMaterializer, cp.ClrType))
            : Convert(visitedMaterializer, cp.ClrType);

        return Block(
            [complexMapVariable],
            Assign(complexMapVariable, readComplexMapExpression),
            Assign(complexInit.MemberAccess, complexInstance));
    }

    /// <summary>
    ///     Visits a direct complex-type projection expression (<c>Select(e =&gt; e.Profile)</c>).
    ///     Reads the complex property's map attribute from the current context, pushes it as the
    ///     new attribute context for the inner materializer, and returns the materialized value
    ///     directly (nullable-guarded when the property is nullable).
    /// </summary>
    private Expression VisitComplexTypeProjection(DynamoComplexTypeProjectionExpression projection)
    {
        var cp = projection.ComplexProperty;
        var attributeName = ((IReadOnlyComplexProperty)cp).GetAttributeName();
        var required = !cp.IsNullable;
        var path = $"{cp.DeclaringType.DisplayName()}.{cp.Name}";

        var complexMapVariable = Variable(
            typeof(Dictionary<string, AttributeValue>),
            $"complex_{attributeName}_map");

        var readComplexMapExpression = CreateReadComplexMapExpression(
            _attributeContextStack.Peek(),
            attributeName,
            required,
            path);

        _attributeContextStack.Push(complexMapVariable);
        var visitedMaterializer = Visit(projection.InnerShaper);
        _attributeContextStack.Pop();

        Expression complexInstance = cp.IsNullable
            ? Condition(
                Equal(
                    complexMapVariable,
                    Constant(null, typeof(Dictionary<string, AttributeValue>))),
                Constant(null, cp.ClrType),
                Convert(visitedMaterializer, cp.ClrType))
            : Convert(visitedMaterializer, cp.ClrType);

        return Block(
            [complexMapVariable],
            Assign(complexMapVariable, readComplexMapExpression),
            complexInstance);
    }

    /// <summary>
    ///     Visits a direct complex-collection projection expression by reading the list attribute
    ///     from the current context and materializing each element map into the target CLR
    ///     collection type.
    /// </summary>
    private Expression VisitComplexCollectionProjection(
        DynamoComplexCollectionProjectionExpression projection)
        => VisitComplexCollectionCore(projection.ComplexProperty, projection.ElementInnerShaper);

    /// <summary>
    ///     Visits complex collection initialization markers by reading the L attribute and
    ///     materializing each element map under its own pushed context.
    /// </summary>
    private Expression VisitComplexCollectionInitialization(
        DynamoComplexCollectionInitializationExpression complexCollInit)
        => Assign(
            complexCollInit.MemberAccess,
            VisitComplexCollectionCore(
                complexCollInit.ComplexProperty,
                complexCollInit.ElementInjectedMaterializer));

    /// <summary>
    ///     Reads a complex collection from the current attribute context and materializes it into
    ///     the configured CLR collection type.
    /// </summary>
    private Expression VisitComplexCollectionCore(
        IComplexProperty complexProperty,
        Expression elementMaterializer)
    {
        var attributeName = ((IReadOnlyComplexProperty)complexProperty).GetAttributeName();
        var path = $"{complexProperty.DeclaringType.DisplayName()}.{complexProperty.Name}";

        if (!DynamoTypeMappingSource.TryGetComplexCollectionElementType(
            complexProperty.ClrType,
            out var elementType))
            throw new InvalidOperationException(
                $"Complex collection '{path}' CLR type '{complexProperty.ClrType.Name}' is not a supported "
                + "collection type. Use List<T> or IList<T>.");

        var wireListVariable =
            Variable(typeof(List<AttributeValue>), $"complexList_{attributeName}");
        var resultListType = typeof(List<>).MakeGenericType(elementType);
        var required = !complexProperty.IsNullable;

        var readListExpression = CreateReadComplexListExpression(
            _attributeContextStack.Peek(),
            attributeName,
            required,
            path);

        var avParameter = Parameter(typeof(AttributeValue), "complexElementAv");
        var elementMapVariable = Variable(
            typeof(Dictionary<string, AttributeValue>),
            $"complexElement_{attributeName}_map");

        _attributeContextStack.Push(elementMapVariable);
        var visitedElementMaterializer = Visit(elementMaterializer);
        _attributeContextStack.Pop();

        if (visitedElementMaterializer.Type != elementType)
            visitedElementMaterializer = Convert(visitedElementMaterializer, elementType);

        // Validate each element is a non-null map.
        var avIsNull = OrElse(
            Equal(avParameter, Constant(null, typeof(AttributeValue))),
            Equal(
                Property(avParameter, AttributeValueNullProperty),
                Constant(true, typeof(bool?))));

        var mapIsNull = Equal(
            elementMapVariable,
            Constant(null, typeof(Dictionary<string, AttributeValue>)));

        var lambdaBody = Block(
            [elementMapVariable],
            IfThen(
                avIsNull,
                Throw(
                    New(
                        InvalidOperationExceptionCtor,
                        Constant(
                            $"Complex collection '{path}' contains NULL element. "
                            + "Elements must be map (M) values.")))),
            Assign(elementMapVariable, Property(avParameter, AttributeValueMProperty)),
            IfThen(
                mapIsNull,
                Throw(
                    New(
                        InvalidOperationExceptionCtor,
                        Constant(
                            $"Complex collection '{path}' contains an element that is not a map (M).")))),
            visitedElementMaterializer);

        var entitiesEnumerable = Call(
            EnumerableMethods.Select.MakeGenericMethod(typeof(AttributeValue), elementType),
            wireListVariable,
            Lambda(lambdaBody, avParameter));

        var entitiesExpression = Call(
            EnumerableMethods.ToList.MakeGenericMethod(elementType),
            entitiesEnumerable);

        Expression populatedResult =
            ConvertCollectionMaterialization(entitiesExpression, complexProperty.ClrType);
        Expression missingResult = complexProperty.IsNullable
            ? Constant(null, complexProperty.ClrType)
            : ConvertCollectionMaterialization(New(resultListType), complexProperty.ClrType);

        var collectionExpression = Block(
            [wireListVariable],
            Assign(wireListVariable, readListExpression),
            Condition(
                Equal(wireListVariable, Constant(null, typeof(List<AttributeValue>))),
                missingResult,
                populatedResult));
        return collectionExpression;
    }

    /// <summary>
    ///     Intercepts ValueBufferTryReadValue calls and replaces them with inline expression trees
    ///     that extract values from Dictionary&lt;string, AttributeValue&gt; with zero boxing overhead.
    /// </summary>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(EF)
            && node.Method.Name == nameof(EF.Property)
            && node.Arguments.Count == 2
            && node.Arguments[1] is ConstantExpression { Value: string propertyName })
        {
            var instanceExpression = Visit(node.Arguments[0]);
            if (instanceExpression == QueryCompilationContext.NotTranslatedExpression
                || instanceExpression == null)
                return QueryCompilationContext.NotTranslatedExpression;

            var memberExpression =
                instanceExpression.Type.GetProperty(propertyName) is not null
                    ?
                    Property(instanceExpression, propertyName)
                    : instanceExpression.Type.GetField(propertyName) is not null
                        ? Field(instanceExpression, propertyName)
                        : QueryCompilationContext.NotTranslatedExpression;

            if (memberExpression == QueryCompilationContext.NotTranslatedExpression)
                return QueryCompilationContext.NotTranslatedExpression;

            if (!instanceExpression.Type.IsValueType)
                memberExpression = Condition(
                    Equal(instanceExpression, Constant(null, instanceExpression.Type)),
                    Default(memberExpression.Type),
                    memberExpression);

            return memberExpression.Type != node.Type
                ? Convert(memberExpression, node.Type)
                : memberExpression;
        }

        if (node.Method.IsGenericMethod)
        {
            var genericMethod = node.Method.GetGenericMethodDefinition();

            if (genericMethod == ExpressionExtensions.ValueBufferTryReadValueMethod)
            {
                var property = (IProperty)((ConstantExpression)node.Arguments[2]).Value!;
                var targetType = node.Type == typeof(object) ? property.ClrType : node.Type;

                // Runtime-only properties are not stored in the DynamoDB item dictionary.
                // They must be materialized from query/runtime context.
                if (property.IsRuntimeOnly())
                {
                    var runtimeValueSource = property.GetRuntimeValueSource();
                    if (string.IsNullOrEmpty(runtimeValueSource))
                        throw new InvalidOperationException(
                            $"Runtime-only property '{property.DeclaringType.DisplayName()}.{property.Name}' "
                            + "is missing a runtime value source annotation.");

                    if (!RuntimeValueSourceFactories.TryGetValue(
                        runtimeValueSource,
                        out var runtimeValueFactory))
                        throw new InvalidOperationException(
                            $"Runtime-only property '{property.DeclaringType.DisplayName()}.{property.Name}' "
                            + $"references unknown runtime value source '{runtimeValueSource}'.");

                    var runtimeValueExpression = runtimeValueFactory(
                        QueryCompilationContext.QueryContextParameter);
                    if (!property.ClrType.IsAssignableFrom(runtimeValueExpression.Type))
                        throw new InvalidOperationException(
                            $"Runtime-only property '{property.DeclaringType.DisplayName()}.{property.Name}' "
                            + $"of type '{property.ClrType.ShortDisplayName()}' cannot be bound from runtime "
                            + $"value source '{runtimeValueSource}' of type "
                            + $"'{runtimeValueExpression.Type.ShortDisplayName()}'.");

                    var runtimeBoundExpression = runtimeValueExpression.Type == property.ClrType
                        ? runtimeValueExpression
                        : Convert(runtimeValueExpression, property.ClrType);

                    return runtimeBoundExpression.Type != node.Type
                        ? Convert(runtimeBoundExpression, node.Type)
                        : runtimeBoundExpression;
                }

                // Get type mapping for converter support
                var typeMapping = property.GetTypeMapping();

                // Strict requiredness, aligned with relational and Mongo providers.
                var required = !property.IsNullable;
                var entityTypeDisplayName = property.DeclaringType.DisplayName();

                // Build inline expression: item.TryGetValue(...) ? value : default
                var valueExpression = CreateGetValueExpression(
                    property.GetAttributeName(),
                    targetType,
                    typeMapping,
                    required,
                    entityTypeDisplayName,
                    property);

                return valueExpression.Type != node.Type
                    ? Convert(valueExpression, node.Type)
                    : valueExpression;
            }
        }

        return base.VisitMethodCall(node);
    }

    /// <summary>
    ///     Wraps a member-access result in a null check when the containing instance can be
    ///     <see langword="null" />, returning the default member value instead of throwing during
    ///     complex-property projection materialization.
    /// </summary>
    /// <param name="instanceExpression">The visited instance expression that owns the accessed member.</param>
    /// <param name="memberAccess">The rewritten member-access expression.</param>
    /// <param name="memberType">The accessed member CLR type.</param>
    /// <returns>The original access or a null-propagating conditional wrapper.</returns>
    private static Expression ApplyNullPropagation(
        Expression instanceExpression,
        Expression memberAccess,
        Type memberType)
    {
        if (instanceExpression.Type.IsValueType
            && Nullable.GetUnderlyingType(instanceExpression.Type) == null)
            return memberAccess;

        return Condition(
            Equal(instanceExpression, Constant(null, instanceExpression.Type)),
            Default(memberType),
            memberAccess);
    }

    /// <summary>Determines whether member access should null-propagate for a materialized receiver type.</summary>
    private static bool ShouldApplyNullPropagation(Type instanceType)
    {
        if (instanceType == typeof(string) || instanceType == typeof(byte[]))
            return false;

        if (instanceType.IsValueType
            || DynamoTypeMappingSource.IsPrimitiveType(instanceType)
            || DynamoTypeMappingSource.IsSupportedPrimitiveCollectionShape(instanceType))
            return false;

        return true;
    }

    /// <summary>
    ///     Builds an expression tree that extracts a typed value from Dictionary&lt;string,
    ///     AttributeValue&gt; with null handling, wire primitive extraction, and inlined EF Core converter
    ///     application.
    /// </summary>
    /// <remarks>
    ///     Expression structure:
    ///     <list type="number">
    ///         <item>Dictionary.TryGetValue(propertyName, out attributeValue)</item>
    ///         <item>Check attributeValue.NULL flag</item>
    ///         <item>Extract wire primitive (attributeValue.S, .N, .BOOL, etc.)</item>
    ///         <item>Inline EF Core converter expression tree (if present)</item>
    ///         <item>Return typed value with zero boxing</item>
    ///     </list>
    /// </remarks>
    private Expression CreateGetValueExpression(
        string propertyName,
        Type type,
        CoreTypeMapping? typeMapping,
        bool required,
        string? entityTypeDisplayName,
        IProperty? property,
        Expression? contextOverride = null)
    {
        var itemParameter = contextOverride ?? _attributeContextStack.Peek();
        var dynamoTypeMapping = typeMapping as DynamoTypeMapping;

        var attributeValueVariable = Variable(typeof(AttributeValue), "attributeValue");

        // item.TryGetValue("PropertyName", out attributeValue)
        var tryGetValueExpression = Call(
            itemParameter,
            DictionaryTryGetValueMethod,
            Constant(propertyName),
            attributeValueVariable);

        var propertyPath = string.IsNullOrWhiteSpace(entityTypeDisplayName)
            ? propertyName
            : $"{entityTypeDisplayName}.{propertyName}";

        var missingReturnExpression = required
            ? CreateThrow(
                $"Required property '{propertyPath}' was not present in the DynamoDB item.")
            : Default(type);

        var nullReturnExpression = required
            ? CreateThrow($"Required property '{propertyPath}' was set to DynamoDB NULL.")
            : Default(type);

        // attributeValue is null OR attributeValue.NULL == true
        var isAttributeValueNullExpression = Equal(
            attributeValueVariable,
            Constant(null, typeof(AttributeValue)));

        // Guard: access to .NULL property would throw NullReferenceException if the
        // AttributeValue itself is null (item absent from projection). Check for null
        // first so the outer OrElse short-circuits before reading the flag.
        var isNullFlagExpression = AndAlso(
            NotEqual(attributeValueVariable, Constant(null, typeof(AttributeValue))),
            Equal(
                Property(attributeValueVariable, AttributeValueNullProperty),
                Constant(true, typeof(bool?))));

        var isDynamoNullExpression = OrElse(isAttributeValueNullExpression, isNullFlagExpression);

        if (dynamoTypeMapping == null)
            throw new InvalidOperationException(
                $"Property '{propertyPath}' does not have a DynamoTypeMapping. "
                + $"All mapped properties must resolve to a DynamoTypeMapping; got '{typeMapping?.GetType().Name ?? "null"}'.");

        var valueExpression = dynamoTypeMapping.CreateReadExpression(
            attributeValueVariable,
            propertyPath,
            required,
            property);

        // Condition branches must agree on the exact CLR type. Mapping-owned readers may return a
        // nullable-adapted or provider-compatible expression that still needs normalization here.
        if (valueExpression.Type != type)
            valueExpression = Convert(valueExpression, type);

        // item.TryGetValue(...) ? (isDynamoNull ? (required?throw:default) : value) :
        // (required?throw:default)
        var completeExpression = Condition(
            tryGetValueExpression,
            Condition(isDynamoNullExpression, nullReturnExpression, valueExpression),
            missingReturnExpression);

        return Block([attributeValueVariable], completeExpression);

        Expression CreateThrow(string message)
            => Throw(New(InvalidOperationExceptionCtor, Constant(message)), type);
    }

    /// <summary>
    ///     Builds an expression that reads a complex property reference from a map attribute
    ///     (<c>AttributeValue.M</c>) and validates null/missing shape semantics.
    /// </summary>
    private static Expression CreateReadComplexMapExpression(
        Expression parentMapExpression,
        string attributeName,
        bool required,
        string path)
    {
        var attributeValueVariable = Variable(typeof(AttributeValue), "complexRefAv");

        var tryGetValueExpression = Call(
            parentMapExpression,
            DictionaryTryGetValueMethod,
            Constant(attributeName),
            attributeValueVariable);

        var isNullExpression = OrElse(
            Equal(attributeValueVariable, Constant(null, typeof(AttributeValue))),
            Equal(
                Property(attributeValueVariable, AttributeValueNullProperty),
                Constant(true, typeof(bool?))));

        var mapExpression = Property(attributeValueVariable, AttributeValueMProperty);

        Expression missingExpression = required
            ? Throw(
                New(
                    InvalidOperationExceptionCtor,
                    Constant($"Required complex property '{path}' is missing or NULL.")),
                typeof(Dictionary<string, AttributeValue>))
            : Constant(null, typeof(Dictionary<string, AttributeValue>));

        Expression wrongShapeExpression = required
            ? Throw(
                New(
                    InvalidOperationExceptionCtor,
                    Constant($"Complex property '{path}' attribute is not a map (M).")),
                typeof(Dictionary<string, AttributeValue>))
            : Constant(null, typeof(Dictionary<string, AttributeValue>));

        var resultExpression = Condition(
            Not(tryGetValueExpression),
            missingExpression,
            Condition(
                isNullExpression,
                missingExpression,
                Condition(
                    Equal(
                        mapExpression,
                        Constant(null, typeof(Dictionary<string, AttributeValue>))),
                    wrongShapeExpression,
                    mapExpression)));

        return Block([attributeValueVariable], resultExpression);
    }

    /// <summary>
    ///     Builds an expression that reads a complex collection from a list attribute
    ///     (<c>AttributeValue.L</c>) and validates null/missing shape semantics.
    /// </summary>
    private static Expression CreateReadComplexListExpression(
        Expression parentMapExpression,
        string attributeName,
        bool required,
        string path)
    {
        var attributeValueVariable = Variable(typeof(AttributeValue), "complexListAv");

        var tryGetValueExpression = Call(
            parentMapExpression,
            DictionaryTryGetValueMethod,
            Constant(attributeName),
            attributeValueVariable);

        var isNullExpression = OrElse(
            Equal(attributeValueVariable, Constant(null, typeof(AttributeValue))),
            Equal(
                Property(attributeValueVariable, AttributeValueNullProperty),
                Constant(true, typeof(bool?))));

        var listExpression = Property(attributeValueVariable, AttributeValueLProperty);

        Expression missingExpression = required
            ? Throw(
                New(
                    InvalidOperationExceptionCtor,
                    Constant($"Required complex collection '{path}' is missing or NULL.")),
                typeof(List<AttributeValue>))
            : Constant(null, typeof(List<AttributeValue>));

        Expression wrongShapeExpression = Throw(
            New(
                InvalidOperationExceptionCtor,
                Constant($"Complex collection '{path}' attribute is not a list (L).")),
            typeof(List<AttributeValue>));

        var resultExpression = Condition(
            Not(tryGetValueExpression),
            missingExpression,
            Condition(
                isNullExpression,
                missingExpression,
                Condition(
                    Equal(listExpression, Constant(null, typeof(List<AttributeValue>))),
                    wrongShapeExpression,
                    listExpression)));

        return Block([attributeValueVariable], resultExpression);
    }

    /// <summary>Converts navigation materialization expressions to the requested collection CLR shape.</summary>
    private static Expression ConvertCollectionMaterialization(
        Expression expression,
        Type targetType)
    {
        if (expression.Type == targetType)
            return expression;

        if (!DynamoTypeMappingSource.TryGetComplexCollectionElementType(
            targetType,
            out var elementType))
            return expression.Type != targetType ? Convert(expression, targetType) : expression;

        var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
        var enumerableExpression = expression;

        if (!enumerableType.IsAssignableFrom(enumerableExpression.Type))
            enumerableExpression = Convert(enumerableExpression, enumerableType);

        var toListMethod = EnumerableMethods.ToList.MakeGenericMethod(elementType);
        var listExpression = Call(toListMethod, enumerableExpression);
        return listExpression.Type == targetType
            ? listExpression
            : Convert(listExpression, targetType);
    }

    /// <summary>Determines whether a CLR type is a non-nullable value type.</summary>
    private static bool IsNonNullableValueType(Type type)
        => type.IsValueType && Nullable.GetUnderlyingType(type) == null;

    /// <summary>Determines whether a type is directly read from DynamoDB primitive wire members.</summary>
    private static bool IsWirePrimitiveType(Type type)
    {
        var nonNullableType = Nullable.GetUnderlyingType(type) ?? type;

        return nonNullableType == typeof(string)
            || nonNullableType == typeof(bool)
            || nonNullableType == typeof(byte[])
            || nonNullableType == typeof(short)
            || nonNullableType == typeof(ushort)
            || nonNullableType == typeof(sbyte)
            || nonNullableType == typeof(byte)
            || nonNullableType == typeof(int)
            || nonNullableType == typeof(uint)
            || nonNullableType == typeof(long)
            || nonNullableType == typeof(ulong)
            || nonNullableType == typeof(float)
            || nonNullableType == typeof(double)
            || nonNullableType == typeof(decimal);
    }

    /// <summary>
    ///     Builds an expression tree that deserializes AttributeValue wire format to a CLR primitive
    ///     type.
    /// </summary>
    /// <remarks>
    ///     Maps AttributeValue properties to wire primitive CLR types:
    ///     <list type="bullet">
    ///         <item>string → attributeValue.S</item>
    ///         <item>bool → attributeValue.BOOL ?? false (non-nullable only)</item>
    ///         <item>numeric primitives → Parse(attributeValue.N, InvariantCulture)</item>
    ///         <item>byte[] → attributeValue.B?.ToArray()</item>
    ///     </list>
    /// </remarks>
    private static Expression CreateAttributeValueToPrimitiveExpression(
        Expression attributeValueExpression,
        Type primitiveType,
        bool allowNullBool)
    {
        // attributeValue.S
        if (primitiveType == typeof(string))
            return Property(attributeValueExpression, AttributeValueSProperty);

        // attributeValue.BOOL (nullable) or attributeValue.BOOL ?? false
        if (primitiveType == typeof(bool))
        {
            var boolProperty = Property(attributeValueExpression, AttributeValueBoolProperty);
            return allowNullBool ? boolProperty : Coalesce(boolProperty, Constant(false));
        }

        // attributeValue.B == null ? null : attributeValue.B.ToArray()
        if (primitiveType == typeof(byte[]))
        {
            var bProperty = Property(attributeValueExpression, AttributeValueBProperty);
            return Condition(
                Equal(bProperty, Constant(null, typeof(MemoryStream))),
                Constant(null, typeof(byte[])),
                Call(bProperty, MemoryStreamToArrayMethod));
        }

        var nProperty = Property(attributeValueExpression, AttributeValueNProperty);

        if (primitiveType == typeof(short)
            || primitiveType == typeof(ushort)
            || primitiveType == typeof(sbyte)
            || primitiveType == typeof(byte)
            || primitiveType == typeof(int)
            || primitiveType == typeof(uint)
            || primitiveType == typeof(long)
            || primitiveType == typeof(ulong)
            || primitiveType == typeof(float)
            || primitiveType == typeof(double)
            || primitiveType == typeof(decimal))
            return CreateNumericStringParseExpression(nProperty, primitiveType);

        throw new InvalidOperationException(
            $"Cannot create expression for AttributeValue to primitive type '{primitiveType.Name}'. "
            + "Supported types: string, bool, numeric types (int, long, float, double, decimal, etc.), and byte[].");
    }

    /// <summary>
    ///     Returns the expected primitive <c>AttributeValue</c> wire member for a wire CLR
    ///     type.
    /// </summary>
    private static string GetExpectedWireMemberName(Type wireType)
        => wireType == typeof(string) ? nameof(AttributeValue.S) :
            wireType == typeof(bool) ? nameof(AttributeValue.BOOL) :
            wireType == typeof(byte[]) ? nameof(AttributeValue.B) : nameof(AttributeValue.N);

    /// <summary>Builds an expression that checks whether the expected primitive wire member is present.</summary>
    private static bool TryCreateHasWireValueExpression(
        Expression attributeValueExpression,
        Type wireType,
        out Expression hasWireValueExpression)
    {
        if (wireType == typeof(string))
        {
            hasWireValueExpression = NotEqual(
                Property(attributeValueExpression, AttributeValueSProperty),
                Constant(null, typeof(string)));
            return true;
        }

        if (wireType == typeof(bool))
        {
            hasWireValueExpression = NotEqual(
                Property(attributeValueExpression, AttributeValueBoolProperty),
                Constant(null, typeof(bool?)));
            return true;
        }

        if (wireType == typeof(byte[]))
        {
            hasWireValueExpression = NotEqual(
                Property(attributeValueExpression, AttributeValueBProperty),
                Constant(null, typeof(MemoryStream)));
            return true;
        }

        // All numeric wire primitives use AttributeValue.N (string)
        hasWireValueExpression = NotEqual(
            Property(attributeValueExpression, AttributeValueNProperty),
            Constant(null, typeof(string)));
        return true;
    }

    /// <summary>
    ///     Builds a typed collection materialization expression for strict list/set/dictionary
    ///     shapes.
    /// </summary>
    private static Expression CreateCollectionValueExpression(
        Expression attributeValueExpression,
        Type targetType,
        CoreTypeMapping? typeMapping,
        string propertyPath,
        bool required,
        IProperty? property)
    {
        if (DynamoTypeMappingSource.TryGetDictionaryValueType(
            targetType,
            out var valueType,
            out var readOnly))
            return CreateDictionaryMaterializationExpression(
                attributeValueExpression,
                targetType,
                valueType,
                readOnly,
                typeMapping?.ElementTypeMapping,
                propertyPath,
                required,
                property);

        if (DynamoTypeMappingSource.TryGetSetElementType(targetType, out var setElementType))
            return CreateSetMaterializationExpression(
                attributeValueExpression,
                targetType,
                setElementType,
                typeMapping?.ElementTypeMapping,
                propertyPath,
                required,
                property);

        if (DynamoTypeMappingSource.TryGetListElementType(targetType, out var listElementType))
            return CreateListMaterializationExpression(
                attributeValueExpression,
                targetType,
                listElementType,
                typeMapping?.ElementTypeMapping,
                propertyPath,
                required,
                property);

        return Default(targetType);
    }

    /// <summary>
    ///     Builds a typed conversion expression from <c>AttributeValue</c> to a model CLR
    ///     value.
    /// </summary>
    private static Expression CreateTypedValueExpressionFromAttributeValue(
        Expression attributeValueExpression,
        Type modelType,
        CoreTypeMapping? typeMapping,
        string propertyPath,
        bool required)
    {
        if (typeMapping is DynamoTypeMapping dynamoTypeMapping)
        {
            var valueExpression = dynamoTypeMapping.CreateReadExpression(
                attributeValueExpression,
                propertyPath,
                required,
                null);

            return valueExpression.Type != modelType
                ? Convert(valueExpression, modelType)
                : valueExpression;
        }

        // All element/value type mappings must be DynamoTypeMapping. A non-DynamoDB mapping
        // reaching here means something was misconfigured in the type mapping source.
        throw new InvalidOperationException(
            $"Collection element or map value for property '{propertyPath}' does not have a DynamoTypeMapping. "
            + $"Got '{typeMapping?.GetType().Name ?? "null"}'.");
    }

    /// <summary>
    ///     Builds a typed conversion expression from provider value to model value with inlined
    ///     converter.
    /// </summary>
    private static Expression CreateTypedValueExpressionFromProvider(
        Expression providerValueExpression,
        Type modelType,
        CoreTypeMapping? typeMapping)
    {
        var converter = typeMapping?.Converter;
        var providerType = converter?.ProviderClrType ?? modelType;

        if (providerValueExpression.Type != providerType)
            providerValueExpression = Convert(providerValueExpression, providerType);

        var modelValueExpression = providerValueExpression;
        if (converter != null)
            modelValueExpression = ReplacingExpressionVisitor.Replace(
                converter.ConvertFromProviderExpression.Parameters.Single(),
                providerValueExpression,
                converter.ConvertFromProviderExpression.Body);

        return modelValueExpression.Type != modelType
            ? Convert(modelValueExpression, modelType)
            : modelValueExpression;
    }

    /// <summary>Builds typed list materialization for <c>AttributeValue.L</c> without boxing.</summary>
    private static Expression CreateListMaterializationExpression(
        Expression attributeValueExpression,
        Type targetType,
        Type elementType,
        CoreTypeMapping? elementMapping,
        string propertyPath,
        bool required,
        IProperty? property)
    {
        var wireListVariable = Variable(typeof(List<AttributeValue>), "wireList");
        var resultListType = typeof(List<>).MakeGenericType(elementType);
        var resultVariable = Variable(resultListType, "result");
        var indexVariable = Variable(typeof(int), "index");
        var countVariable = Variable(typeof(int), "count");

        var assignWireList = Assign(
            wireListVariable,
            Property(attributeValueExpression, AttributeValueLProperty));
        Expression missingWireExpression = required
            ? Throw(
                New(
                    InvalidOperationExceptionCtor,
                    Constant(
                        $"Required property '{propertyPath}' did not contain a value for expected DynamoDB wire member '{nameof(AttributeValue.L)}'.")),
                targetType)
            : Default(targetType);

        var ctor = resultListType.GetConstructor([typeof(int)])!;
        var addMethod = resultListType.GetMethod(nameof(List<int>.Add), [elementType])!;
        var toArrayMethod = resultListType.GetMethod(nameof(List<int>.ToArray), Type.EmptyTypes)!;

        var assignResult = Assign(
            resultVariable,
            New(ctor, Property(wireListVariable, nameof(List<AttributeValue>.Count))));
        var assignCount = Assign(
            countVariable,
            Property(wireListVariable, nameof(List<AttributeValue>.Count)));
        var assignIndex = Assign(indexVariable, Constant(0));

        var elementAttributeValueExpression = Property(wireListVariable, "Item", indexVariable);
        var elementRequired = IsRequiredCollectionElement(property, elementType);
        var elementExpression = CreateTypedValueExpressionFromAttributeValue(
            elementAttributeValueExpression,
            elementType,
            elementMapping,
            propertyPath,
            elementRequired);

        var loopBreak = Label("ListLoopBreak");
        var loop = Loop(
            IfThenElse(
                LessThan(indexVariable, countVariable),
                Block(
                    Call(resultVariable, addMethod, elementExpression),
                    PostIncrementAssign(indexVariable)),
                Break(loopBreak)),
            loopBreak);

        Expression resultExpression = targetType.IsArray
            ? Call(resultVariable, toArrayMethod)
            : resultVariable;

        if (resultExpression.Type != targetType)
            resultExpression = Convert(resultExpression, targetType);

        var populateBlock = Block(
            [resultVariable, indexVariable, countVariable],
            assignResult,
            assignCount,
            assignIndex,
            loop,
            resultExpression);

        return Block(
            [wireListVariable],
            assignWireList,
            Condition(
                Equal(wireListVariable, Constant(null, typeof(List<AttributeValue>))),
                missingWireExpression,
                populateBlock));
    }

    /// <summary>Builds typed dictionary materialization for <c>AttributeValue.M</c> without boxing.</summary>
    private static Expression CreateDictionaryMaterializationExpression(
        Expression attributeValueExpression,
        Type targetType,
        Type valueType,
        bool readOnly,
        CoreTypeMapping? valueMapping,
        string propertyPath,
        bool required,
        IProperty? property)
    {
        var wireMapType = typeof(Dictionary<string, AttributeValue>);
        var wireMapVariable = Variable(wireMapType, "wireMap");
        var resultType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
        var resultVariable = Variable(resultType, "result");
        var enumeratorType =
            wireMapType.GetMethod(nameof(Dictionary<string, AttributeValue>.GetEnumerator))!
                .ReturnType;
        var enumeratorVariable = Variable(enumeratorType, "enumerator");
        var currentType = typeof(KeyValuePair<string, AttributeValue>);
        var currentVariable = Variable(currentType, "current");

        var assignWireMap = Assign(
            wireMapVariable,
            Property(attributeValueExpression, AttributeValueMProperty));
        Expression missingWireExpression = required
            ? Throw(
                New(
                    InvalidOperationExceptionCtor,
                    Constant(
                        $"Required property '{propertyPath}' did not contain a value for expected DynamoDB wire member '{nameof(AttributeValue.M)}'.")),
                targetType)
            : Default(targetType);

        var ctor = resultType.GetConstructor([typeof(int), typeof(IEqualityComparer<string>)])!;
        var addMethod = resultType.GetMethod(
            nameof(Dictionary<string, int>.Add),
            [typeof(string), valueType])!;
        var assignResult = Assign(
            resultVariable,
            New(
                ctor,
                Property(wireMapVariable, nameof(Dictionary<string, AttributeValue>.Count)),
                Constant(StringComparer.Ordinal, typeof(IEqualityComparer<string>))));

        var assignEnumerator = Assign(
            enumeratorVariable,
            Call(
                wireMapVariable,
                wireMapType.GetMethod(nameof(Dictionary<string, AttributeValue>.GetEnumerator))!));

        var valueRequired = IsRequiredCollectionElement(property, valueType);
        var valueExpression = CreateTypedValueExpressionFromAttributeValue(
            Property(currentVariable, nameof(KeyValuePair<string, AttributeValue>.Value)),
            valueType,
            valueMapping,
            propertyPath,
            valueRequired);

        var loopBreak = Label("DictionaryLoopBreak");
        var loop = Loop(
            IfThenElse(
                Call(enumeratorVariable, enumeratorType.GetMethod(nameof(IEnumerator.MoveNext))!),
                Block(
                    Assign(
                        currentVariable,
                        Property(
                            enumeratorVariable,
                            nameof(IEnumerator<KeyValuePair<string, AttributeValue>>.Current))),
                    Call(
                        resultVariable,
                        addMethod,
                        Property(currentVariable, nameof(KeyValuePair<string, AttributeValue>.Key)),
                        valueExpression)),
                Break(loopBreak)),
            loopBreak);

        Expression dictionaryResultExpression = resultVariable;
        if (readOnly)
        {
            var readOnlyType =
                typeof(ReadOnlyDictionary<,>).MakeGenericType(typeof(string), valueType);
            var readOnlyCtor =
                readOnlyType.GetConstructor(
                    [typeof(IDictionary<,>).MakeGenericType(typeof(string), valueType)])!;
            dictionaryResultExpression = New(
                readOnlyCtor,
                Convert(
                    resultVariable,
                    typeof(IDictionary<,>).MakeGenericType(typeof(string), valueType)));
        }

        if (dictionaryResultExpression.Type != targetType)
            dictionaryResultExpression = Convert(dictionaryResultExpression, targetType);

        var populateBlock = Block(
            [resultVariable, enumeratorVariable, currentVariable],
            assignResult,
            assignEnumerator,
            loop,
            dictionaryResultExpression);

        return Block(
            [wireMapVariable],
            assignWireMap,
            Condition(
                Equal(wireMapVariable, Constant(null, wireMapType)),
                missingWireExpression,
                populateBlock));
    }

    /// <summary>Builds typed set materialization for <c>AttributeValue.SS</c>, <c>NS</c>, and <c>BS</c>.</summary>
    private static Expression CreateSetMaterializationExpression(
        Expression attributeValueExpression,
        Type targetType,
        Type elementType,
        CoreTypeMapping? elementMapping,
        string propertyPath,
        bool required,
        IProperty? property)
    {
        var providerType = elementMapping?.Converter?.ProviderClrType ?? elementType;
        var nonNullableProviderType = Nullable.GetUnderlyingType(providerType) ?? providerType;
        var wireProperty =
            nonNullableProviderType == typeof(string) ? AttributeValueSsProperty :
            nonNullableProviderType == typeof(byte[]) ? AttributeValueBsProperty :
            AttributeValueNsProperty;

        var wireListType = nonNullableProviderType == typeof(byte[])
            ? typeof(List<MemoryStream>)
            : typeof(List<string>);

        var wireListVariable = Variable(wireListType, "wireSet");
        var setType = typeof(HashSet<>).MakeGenericType(elementType);
        var setVariable = Variable(setType, "result");
        var indexVariable = Variable(typeof(int), "index");
        var countVariable = Variable(typeof(int), "count");

        var assignWireSet =
            Assign(wireListVariable, Property(attributeValueExpression, wireProperty));
        Expression missingWireExpression = required
            ? Throw(
                New(
                    InvalidOperationExceptionCtor,
                    Constant(
                        $"Required property '{propertyPath}' did not contain a value for expected DynamoDB wire member '{GetExpectedSetWireMemberName(nonNullableProviderType)}'.")),
                targetType)
            : Default(targetType);

        var setCtor = setType.GetConstructor(Type.EmptyTypes)!;
        var addMethod = setType.GetMethod(nameof(HashSet<int>.Add), [elementType])!;
        var assignSet = Assign(setVariable, New(setCtor));
        var assignCount = Assign(countVariable, Property(wireListVariable, "Count"));
        var assignIndex = Assign(indexVariable, Constant(0));

        Expression providerValueExpression;
        if (nonNullableProviderType == typeof(byte[]))
        {
            var memoryStreamExpression = Property(wireListVariable, "Item", indexVariable);
            providerValueExpression = Condition(
                Equal(memoryStreamExpression, Constant(null, typeof(MemoryStream))),
                Constant(null, typeof(byte[])),
                Call(memoryStreamExpression, MemoryStreamToArrayMethod));
        }
        else if (nonNullableProviderType == typeof(string))
        {
            providerValueExpression = Property(wireListVariable, "Item", indexVariable);
        }
        else
        {
            providerValueExpression = CreateNumericStringParseExpression(
                Property(wireListVariable, "Item", indexVariable),
                nonNullableProviderType);
        }

        var elementExpression = CreateTypedValueExpressionFromProvider(
            providerValueExpression,
            elementType,
            elementMapping);

        var loopBreak = Label("SetLoopBreak");
        var loop = Loop(
            IfThenElse(
                LessThan(indexVariable, countVariable),
                Block(
                    Call(setVariable, addMethod, elementExpression),
                    PostIncrementAssign(indexVariable)),
                Break(loopBreak)),
            loopBreak);

        Expression resultExpression = setVariable;
        if (resultExpression.Type != targetType)
            resultExpression = Convert(resultExpression, targetType);

        var populateBlock = Block(
            [setVariable, indexVariable, countVariable],
            assignSet,
            assignCount,
            assignIndex,
            loop,
            resultExpression);

        return Block(
            [wireListVariable],
            assignWireSet,
            Condition(
                Equal(wireListVariable, Constant(null, wireListType)),
                missingWireExpression,
                populateBlock));
    }

    /// <summary>Parses a DynamoDB numeric string into the requested CLR numeric provider type.</summary>
    private static Expression CreateNumericStringParseExpression(
        Expression numericStringExpression,
        Type numericType)
    {
        var parseMethod = GetNumericParseMethod(numericType);

        var numberStyles =
            numericType == typeof(float)
            || numericType == typeof(double)
            || numericType == typeof(decimal)
                ? FloatNumberStylesExpression
                : IntegerNumberStylesExpression;

        return Call(parseMethod, numericStringExpression, numberStyles, InvariantCultureExpression);
    }

    /// <summary>Gets and caches the numeric Parse(string, NumberStyles, IFormatProvider) method.</summary>
    private static MethodInfo GetNumericParseMethod(Type numericType)
        => NumericParseMethodCache.GetOrAdd(
            numericType,
            static type => type.GetMethod(
                    nameof(int.Parse),
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    [typeof(string), typeof(NumberStyles), typeof(IFormatProvider)],
                    null)
                ?? throw new InvalidOperationException(
                    $"Cannot parse DynamoDB numeric string for provider type '{type.Name}'."));

    /// <summary>Determines whether collection elements should be treated as required.</summary>
    private static bool IsRequiredCollectionElement(IProperty? property, Type elementType)
        => property?.GetElementType()?.IsNullable == false || IsNonNullableValueType(elementType);

    /// <summary>Returns the expected set wire member name for a provider element type.</summary>
    private static string GetExpectedSetWireMemberName(Type providerType)
        => providerType == typeof(string) ? nameof(AttributeValue.SS) :
            providerType == typeof(byte[]) ? nameof(AttributeValue.BS) : nameof(AttributeValue.NS);
}
