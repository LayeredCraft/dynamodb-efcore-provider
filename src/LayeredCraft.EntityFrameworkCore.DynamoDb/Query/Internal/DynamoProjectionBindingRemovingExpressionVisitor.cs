using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using static System.Linq.Expressions.Expression;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Replaces EF Core's abstract ProjectionBindingExpression nodes with concrete expression
///     trees that extract property values from Dictionary&lt;string, AttributeValue&gt;.
/// </summary>
/// <remarks>
///     Builds expression trees at query compilation time, inlining all type conversions to
///     eliminate runtime boxing. The compiled query executes AttributeValue deserialization and EF
///     Core value conversions as pure IL with zero boxing overhead.
/// </remarks>
public class DynamoProjectionBindingRemovingExpressionVisitor(
    ParameterExpression itemParameter,
    SelectExpression selectExpression,
    IModel model,
    Func<Expression, Expression>? injectStructuralTypeMaterializers = null) : ExpressionVisitor
{
    private readonly HashSet<Type> _ownedClrTypes =
        model
            .GetEntityTypes()
            .Where(entityType => entityType.IsOwned())
            .Select(entityType => entityType.ClrType)
            .ToHashSet();

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

    private static readonly MethodInfo PopulateCollectionMethodInfo =
        typeof(DynamoProjectionBindingRemovingExpressionVisitor).GetMethod(
            nameof(PopulateCollection),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo PopulateCollectionOnOwnerMethodInfo =
        typeof(DynamoProjectionBindingRemovingExpressionVisitor).GetMethod(
            nameof(PopulateCollectionOnOwner),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private readonly Stack<ParameterExpression> _attributeContextStack = new([itemParameter]);
    private readonly Stack<ParameterExpression> _ownerAttributeContextStack = new();
    private readonly Dictionary<ParameterExpression, Expression> _ordinalParameterBindings = new();

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
    ///     Handles ProjectionBindingExpression for custom Select projections. Converts member-based
    ///     bindings to indexed dictionary access and supports index-based bindings.
    /// </summary>
    protected override Expression VisitExtension(Expression node)
    {
        if (node is StructuralTypeShaperExpression shaperExpression
            && shaperExpression.StructuralType is IEntityType entityType
            && entityType.IsOwned())
            return VisitOwnedStructuralTypeShaperExpression(shaperExpression, entityType);

        if (node is DynamoCollectionShaperExpression collectionShaperExpression
            && collectionShaperExpression.Navigation is INavigation collectionNavigation
            && collectionNavigation.IsEmbedded())
        {
            var containingAttributeName =
                collectionShaperExpression.Projection is DynamoObjectArrayProjectionExpression
                    objectArrayProjection
                    ? objectArrayProjection.AttributeName
                    : collectionNavigation.TargetEntityType.GetContainingAttributeName()
                    ?? collectionNavigation.Name;

            return CreateOwnedCollectionMaterializationExpression(
                collectionNavigation,
                null,
                collectionShaperExpression.InnerShaper,
                containingAttributeName);
        }

        if (node is MaterializeCollectionNavigationExpression
            materializeCollectionNavigationExpression)
        {
            var navigation = materializeCollectionNavigationExpression.Navigation;

            if (navigation is INavigation embeddedNavigation && embeddedNavigation.IsEmbedded())
            {
                var elementShaperExpression =
                    TryExtractElementShaperExpression(
                        materializeCollectionNavigationExpression.Subquery,
                        embeddedNavigation.TargetEntityType)
                    ?? throw new InvalidOperationException(
                        $"Embedded collection navigation '{embeddedNavigation.DeclaringEntityType.DisplayName()}.{embeddedNavigation.Name}' reached projection-binding removal without a '{nameof(DynamoCollectionShaperExpression)}' and without a shaped subquery fallback.");

                return CreateOwnedCollectionMaterializationExpression(
                    embeddedNavigation,
                    null,
                    elementShaperExpression,
                    embeddedNavigation.TargetEntityType.GetContainingAttributeName()
                    ?? embeddedNavigation.Name);
            }

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
                || navigation.DeclaringEntityType.IsOwned()
                || navigation.PropertyInfo is null
                || !navigation.PropertyInfo.CanWrite)
                return entityExpression;

            var entityVariable = Variable(entityExpression.Type, "includedEntity");
            var assignEntity = Assign(entityVariable, entityExpression);
            var visitedNavigationExpression = Visit(includeExpression.NavigationExpression);
            if (visitedNavigationExpression == QueryCompilationContext.NotTranslatedExpression
                || visitedNavigationExpression == null)
                return QueryCompilationContext.NotTranslatedExpression;

            var navigationExpression =
                navigation.TargetEntityType.IsOwned() && navigation.IsEmbedded()
                    ? navigation.IsCollection
                        ? visitedNavigationExpression
                        : CreateOwnedReferenceMaterializationExpression(navigation)
                    : CreateGetValueExpression(
                        navigation.Name,
                        navigation.ClrType,
                        null,
                        false,
                        navigation.DeclaringEntityType.DisplayName(),
                        null);

            var navigationAssignment = Assign(
                Property(entityVariable, navigation.PropertyInfo),
                ConvertCollectionMaterialization(navigationExpression, navigation.ClrType));

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

                // Get projection at this index
                var projection = selectExpression.Projection[index];
                var propertyName = projection.Expression is SqlPropertyExpression propertyExpression
                    ? propertyExpression.PropertyName
                    : projection.Alias;

                // Get type mapping from SQL expression for converter support
                var typeMapping = projection.Expression.TypeMapping;

                // For custom projections, we only have the CLR type, not IProperty metadata.
                // Enforce strict requiredness for non-nullable value types to align with
                // relational-style
                // materialization semantics.
                var required = IsNonNullableValueType(projectionBinding.Type);

                if (typeMapping == null
                    && TryResolveOwnedEmbeddedReferenceNavigation(
                        propertyName,
                        projectionBinding.Type,
                        out var ownedNavigation))
                    return CreateOwnedReferenceMaterializationExpression(ownedNavigation);

                // Use unified code path with converter support
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

                if (typeMapping == null
                    && TryResolveOwnedEmbeddedReferenceNavigation(
                        propertyName,
                        projectionBinding.Type,
                        out var ownedNavigation))
                    return CreateOwnedReferenceMaterializationExpression(ownedNavigation);

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

    /// <summary>Adds null-propagation for member access over owned-reference instances.</summary>
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression == null)
            return base.VisitMember(node);

        var instanceExpression = Visit(node.Expression);
        if (instanceExpression == QueryCompilationContext.NotTranslatedExpression
            || instanceExpression == null)
            return QueryCompilationContext.NotTranslatedExpression;

        var memberExpression = node.Update(instanceExpression);
        if (instanceExpression.Type.IsValueType
            || !_ownedClrTypes.Contains(instanceExpression.Type))
            return memberExpression;

        return Condition(
            Equal(instanceExpression, Constant(null, instanceExpression.Type)),
            Default(node.Type),
            memberExpression);
    }

    /// <summary>
    ///     Visits owned entity shapers by switching the current attribute-map context to the owned
    ///     navigation container map.
    /// </summary>
    private Expression VisitOwnedStructuralTypeShaperExpression(
        StructuralTypeShaperExpression shaperExpression,
        IEntityType entityType)
    {
        var ownership = entityType.FindOwnership();
        if (ownership is { IsUnique: false }
                && _ordinalParameterBindings.ContainsKey(_attributeContextStack.Peek()))
            // A raw StructuralTypeShaperExpression for a collection-element owned type should never
            // reach this visitor: InjectStructuralTypeMaterializers must replace all such shapers
            // with injected block expressions before this visitor runs. If this fires, the pipeline
            // order is wrong or a shaper was constructed without going through injection.
            throw new InvalidOperationException(
                $"Unexpected raw '{nameof(StructuralTypeShaperExpression)}' for collection-element "
                + $"owned type '{entityType.DisplayName()}' encountered during projection-binding "
                + "removal. Ensure 'InjectStructuralTypeMaterializers' runs before this visitor.");

        var containingAttributeName = GetOwnedContainingAttributeName(entityType);
        if (string.IsNullOrWhiteSpace(containingAttributeName))
            return base.VisitExtension(shaperExpression);

        var required = ownership is { IsRequiredDependent: true };
        var navigationPath = ownership?.PrincipalEntityType is null
            ? entityType.DisplayName()
            : $"{ownership.PrincipalEntityType.DisplayName()}.{ownership.PrincipalToDependent?.Name ?? entityType.DisplayName()}";

        var ownedMapVariable = Variable(
            typeof(Dictionary<string, AttributeValue>),
            $"owned_{containingAttributeName}_map");

        var readOwnedMapExpression = CreateReadOwnedMapExpression(
            _attributeContextStack.Peek(),
            containingAttributeName,
            required,
            navigationPath);

        _attributeContextStack.Push(ownedMapVariable);
        var visitedOwnedShaper = base.VisitExtension(shaperExpression);
        _attributeContextStack.Pop();

        var assignOwnedMap = Assign(ownedMapVariable, readOwnedMapExpression);
        if (required)
            return Block([ownedMapVariable], assignOwnedMap, visitedOwnedShaper);

        return Block(
            [ownedMapVariable],
            assignOwnedMap,
            Condition(
                Equal(ownedMapVariable, Constant(null, ownedMapVariable.Type)),
                Constant(null, shaperExpression.Type),
                visitedOwnedShaper));
    }

    /// <summary>Resolves a projected member to an embedded owned reference navigation when unambiguous.</summary>
    private bool TryResolveOwnedEmbeddedReferenceNavigation(
        string propertyName,
        Type projectedType,
        out INavigation navigation)
    {
        navigation = null!;
        INavigation? match = null;

        foreach (var entityType in model.GetEntityTypes())
        {
            foreach (var candidate in entityType.GetNavigations())
            {
                if (candidate.Name != propertyName
                    || candidate.IsCollection
                    || !candidate.IsEmbedded()
                    || !candidate.TargetEntityType.IsOwned())
                    continue;

                if (candidate.ClrType != projectedType
                    && candidate.TargetEntityType.ClrType != projectedType)
                    continue;

                if (match != null)
                    return false;

                match = candidate;
            }
        }

        if (match == null)
            return false;

        navigation = match;
        return true;
    }

    /// <summary>
    ///     Builds an expression that reads an owned reference from a map attribute (
    ///     <see cref="AttributeValue.M" />) and validates null/missing shape semantics.
    /// </summary>
    private static Expression CreateReadOwnedMapExpression(
        Expression parentMapExpression,
        string containingAttributeName,
        bool required,
        string navigationPath)
    {
        var attributeValueVariable = Variable(typeof(AttributeValue), "ownedRefAv");

        var tryGetValueExpression = Call(
            parentMapExpression,
            DictionaryTryGetValueMethod,
            Constant(containingAttributeName),
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
                    Constant($"Required owned navigation '{navigationPath}' is missing or NULL.")),
                typeof(Dictionary<string, AttributeValue>))
            : Constant(null, typeof(Dictionary<string, AttributeValue>));

        Expression wrongShapeExpression = required
            ? Throw(
                New(
                    InvalidOperationExceptionCtor,
                    Constant($"Owned navigation '{navigationPath}' attribute is not a map (M).")),
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

    /// <summary>Gets the configured containing attribute name for an owned entity type.</summary>
    private static string? GetOwnedContainingAttributeName(IEntityType entityType)
        => entityType.GetContainingAttributeName()
            ?? entityType.FindOwnership()?.PrincipalToDependent?.Name;

    /// <summary>
    ///     Builds typed materialization for an owned collection navigation without object casts or
    ///     reflection assignment.
    /// </summary>
    private Expression CreateOwnedCollectionMaterializationExpression(
        INavigation navigation,
        Expression? ownerEntityExpression = null,
        Expression? elementShaperExpression = null,
        string? containingAttributeNameOverride = null)
    {
        if (!DynamoTypeMappingSource.TryGetListElementType(navigation.ClrType, out var elementType))
            throw new InvalidOperationException(
                $"Owned collection '{navigation.DeclaringEntityType.DisplayName()}.{navigation.Name}' CLR type '{navigation.ClrType.Name}' is not a supported collection type.");

        // EF Core does not expose collection accessors for array navigations.
        var collectionAccessor = navigation.ClrType.IsArray
            ? null
            : navigation.GetCollectionAccessor();

        var containingAttributeName = containingAttributeNameOverride
            ?? navigation.TargetEntityType.GetContainingAttributeName() ?? navigation.Name;
        var navigationPath = $"{navigation.DeclaringEntityType.DisplayName()}.{navigation.Name}";
        var required = navigation.ForeignKey.IsRequiredDependent;

        var wireListVariable = Variable(typeof(List<AttributeValue>), "ownedWireList");
        var resultListType = typeof(List<>).MakeGenericType(elementType);

        var assignWireList = Assign(
            wireListVariable,
            CreateReadOwnedListExpression(
                _attributeContextStack.Peek(),
                containingAttributeName,
                required,
                navigationPath));

        var ownerContext = _attributeContextStack.Peek();
        var avParameter = Parameter(typeof(AttributeValue), "ownedElementAv");
        var elementMapVariable = Variable(
            typeof(Dictionary<string, AttributeValue>),
            "ownedElementMap");
        var ordinalParameter = Parameter(typeof(int), "ownedOrdinal");
        var ordinalExpression = Add(ordinalParameter, Constant(1));

        // Element materialization executes under the element-map context, but dependent key
        // propagation still needs access to owner maps and collection ordinals for shadow keys.
        _ownerAttributeContextStack.Push(ownerContext);
        _attributeContextStack.Push(elementMapVariable);
        _ordinalParameterBindings[elementMapVariable] = ordinalExpression;

        Expression elementMaterializationExpression;
        if (elementShaperExpression == null && injectStructuralTypeMaterializers != null)
        {
            // No pre-built shaper was found for this nested collection element type in the
            // current expression tree (e.g. OrderLine inside Order.Lines when the injected
            // Order block does not embed DynamoCollectionShaperExpression(Lines) because
            // StructuralTypeShaperExpression has no navigation sub-expressions). Create a
            // synthetic shaper and inject a full EF Core materializer so that shadow
            // properties — including the shadow ordinal key property — are handled correctly
            // via ISnapshot + StartTracking.
            var syntheticShaper = new StructuralTypeShaperExpression(
                navigation.TargetEntityType,
                Constant(ValueBuffer.Empty),
                false);

            var injectedMaterializer = injectStructuralTypeMaterializers(syntheticShaper);
            elementShaperExpression = injectedMaterializer;
        }

        if (elementShaperExpression == null)
        {
            elementMaterializationExpression = CreateOwnedEntityMaterializationExpression(
                navigation.TargetEntityType,
                navigationPath,
                ordinalExpression,
                true);
        }
        else
        {
            elementMaterializationExpression = Visit(elementShaperExpression);

            if (navigation
                .TargetEntityType
                .GetNavigations()
                .Any(static innerNavigation => innerNavigation.TargetEntityType.IsOwned()))
                elementMaterializationExpression = CreateOwnedNavigationHydrationExpression(
                    navigation.TargetEntityType,
                    elementMaterializationExpression,
                    elementType,
                    elementShaperExpression);
        }

        _ordinalParameterBindings.Remove(elementMapVariable);
        _attributeContextStack.Pop();
        _ownerAttributeContextStack.Pop();

        if (elementMaterializationExpression == QueryCompilationContext.NotTranslatedExpression)
            throw new InvalidOperationException(
                $"Failed to materialize owned collection '{navigationPath}' using the embedded element shaper.");

        if (elementMaterializationExpression.Type != elementType)
            elementMaterializationExpression =
                Convert(elementMaterializationExpression, elementType);

        // Inline validation into the lambda body — eliminates the yield iterator allocation
        // and intermediate IEnumerable<Dictionary<,>> from the old
        // ValidateAndExtractOwnedElementMaps.
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
                            $"Owned collection '{navigationPath}' contains NULL element. Elements must be map (M) values.")))),
            Assign(elementMapVariable, Property(avParameter, AttributeValueMProperty)),
            IfThen(
                mapIsNull,
                Throw(
                    New(
                        InvalidOperationExceptionCtor,
                        Constant(
                            $"Owned collection '{navigationPath}' contains an element that is not a map (M).")))),
            elementMaterializationExpression);

        // Iterate wireList (List<AttributeValue>) directly — no intermediate
        // IEnumerable<Dictionary<,>>.
        var entitiesEnumerable = Call(
            EnumerableMethods.SelectWithOrdinal.MakeGenericMethod(
                typeof(AttributeValue),
                elementType),
            wireListVariable,
            Lambda(lambdaBody, avParameter, ordinalParameter));

        // Eager materialization is required for correctness: elementMapVariable is a block variable
        // (not a lambda parameter) that may be captured by nested owned-navigation lambdas (e.g.,
        // for FK propagation). Consuming the lazy IEnumerable<T> produced by Select after those
        // closures are built causes element duplication; forcing evaluation before any nested
        // consumers run prevents this.
        // For array targets, ToArray() forces evaluation directly; no intermediate List<T> needed.
        // For all other targets, ToList() forces evaluation and ConvertCollectionMaterialization
        // converts the result to the requested CLR type.
        var entitiesExpression = navigation.ClrType.IsArray
            ? Call(EnumerableMethods.ToArray.MakeGenericMethod(elementType), entitiesEnumerable)
            : Call(EnumerableMethods.ToList.MakeGenericMethod(elementType), entitiesEnumerable);

        Expression emptyResultExpression;
        Expression populatedResultExpression;

        if (collectionAccessor == null)
        {
            emptyResultExpression =
                ConvertCollectionMaterialization(New(resultListType), navigation.ClrType);
            populatedResultExpression =
                ConvertCollectionMaterialization(entitiesExpression, navigation.ClrType);
        }
        else
        {
            var populateCollectionMethod = ownerEntityExpression == null
                ? PopulateCollectionMethodInfo.MakeGenericMethod(elementType, navigation.ClrType)
                : PopulateCollectionOnOwnerMethodInfo.MakeGenericMethod(
                    elementType,
                    navigation.ClrType);
            var collectionAccessorExpression =
                Constant(collectionAccessor, typeof(IClrCollectionAccessor));
            var ownerExpression = ownerEntityExpression == null
                ? null
                : Convert(ownerEntityExpression, typeof(object));

            // Keep optional/missing owned collection semantics as empty collection while still
            // routing final collection construction through IClrCollectionAccessor when available.
            emptyResultExpression = ownerExpression == null
                ? Call(populateCollectionMethod, collectionAccessorExpression, New(resultListType))
                : Call(
                    populateCollectionMethod,
                    collectionAccessorExpression,
                    ownerExpression,
                    New(resultListType));
            populatedResultExpression = ownerExpression == null
                ? Call(populateCollectionMethod, collectionAccessorExpression, entitiesExpression)
                : Call(
                    populateCollectionMethod,
                    collectionAccessorExpression,
                    ownerExpression,
                    entitiesExpression);
        }

        return Block(
            [wireListVariable],
            assignWireList,
            Condition(
                Equal(wireListVariable, Constant(null, typeof(List<AttributeValue>))),
                emptyResultExpression,
                populatedResultExpression));
    }

    /// <summary>
    ///     Builds typed materialization for an owned reference navigation by switching to the owned
    ///     map context.
    /// </summary>
    private Expression CreateOwnedReferenceMaterializationExpression(INavigation navigation)
    {
        var containingAttributeName =
            navigation.TargetEntityType.GetContainingAttributeName() ?? navigation.Name;
        var required = navigation.ForeignKey.IsRequiredDependent;
        var navigationPath = $"{navigation.DeclaringEntityType.DisplayName()}.{navigation.Name}";

        var ownedMapVariable = Variable(
            typeof(Dictionary<string, AttributeValue>),
            $"owned_{containingAttributeName}_map");

        var assignOwnedMap = Assign(
            ownedMapVariable,
            CreateReadOwnedMapExpression(
                _attributeContextStack.Peek(),
                containingAttributeName,
                required,
                navigationPath));

        _attributeContextStack.Push(ownedMapVariable);
        var ownedEntityExpression = CreateOwnedEntityMaterializationExpression(
            navigation.TargetEntityType,
            navigationPath,
            null);
        _attributeContextStack.Pop();

        return Block(
            [ownedMapVariable],
            assignOwnedMap,
            Condition(
                Equal(ownedMapVariable, Constant(null, ownedMapVariable.Type)),
                Constant(null, navigation.ClrType),
                Convert(ownedEntityExpression, navigation.ClrType)));
    }

    /// <summary>Hydrates owned navigations on an element that came from a synthetic structural shaper.</summary>
    private Expression CreateOwnedNavigationHydrationExpression(
        IEntityType entityType,
        Expression elementExpression,
        Type elementType,
        Expression sourceShaperExpression)
    {
        var elementVariable = Variable(elementType, $"hydrated_{elementType.Name}");
        List<Expression> hydrationExpressions = [Assign(elementVariable, elementExpression)];

        foreach (var navigation in entityType.GetNavigations())
        {
            if (!navigation.TargetEntityType.IsOwned()
                || navigation.PropertyInfo is null
                || !navigation.PropertyInfo.CanWrite)
                continue;

            var nestedCollectionElementShaper = navigation.IsCollection
                ? TryExtractCollectionNavigationElementShaperExpression(
                    sourceShaperExpression,
                    navigation)
                ?? TryExtractElementShaperExpression(
                    sourceShaperExpression,
                    navigation.TargetEntityType)
                : null;

            var navigationValueExpression = navigation.IsCollection
                ? CreateOwnedCollectionMaterializationExpression(
                    navigation,
                    elementVariable,
                    nestedCollectionElementShaper)
                : CreateOwnedReferenceMaterializationExpression(navigation);

            var memberExpression = Property(elementVariable, navigation.PropertyInfo);
            if (navigationValueExpression.Type != memberExpression.Type)
                navigationValueExpression =
                    Convert(navigationValueExpression, memberExpression.Type);

            hydrationExpressions.Add(Assign(memberExpression, navigationValueExpression));
        }

        hydrationExpressions.Add(elementVariable);
        return Block([elementVariable], hydrationExpressions);
    }

    /// <summary>
    ///     Builds typed owned entity materialization using the current attribute-map context and the
    ///     existing scalar value pipeline.
    /// </summary>
    private Expression CreateOwnedEntityMaterializationExpression(
        IEntityType entityType,
        string navigationPath,
        Expression? ordinalExpression,
        bool includeNavigations = true)
    {
        var constructor = entityType.ClrType.GetConstructor(Type.EmptyTypes);
        if (constructor == null)
            throw new InvalidOperationException(
                $"Could not construct owned CLR type '{entityType.ClrType.Name}'.");

        var instanceVariable = Variable(entityType.ClrType, $"owned_{entityType.ClrType.Name}");
        List<Expression> expressions = [Assign(instanceVariable, New(constructor))];

        foreach (var property in entityType.GetProperties())
        {
            if (property.IsShadowProperty())
                continue;

            var memberInfo = property.PropertyInfo as MemberInfo ?? property.FieldInfo;
            if (memberInfo == null)
                continue;

            var memberType = memberInfo is PropertyInfo propertyInfo
                ? propertyInfo.PropertyType
                : ((FieldInfo)memberInfo).FieldType;

            Expression? valueExpression;
            if (property.IsOwnedOrdinalKeyProperty())
                valueExpression = ordinalExpression;
            else
                valueExpression = CreateGetValueExpression(
                    property.Name,
                    property.ClrType,
                    property.GetTypeMapping(),
                    !property.IsNullable,
                    entityType.DisplayName(),
                    property);

            if (valueExpression == null)
                continue;

            if (valueExpression.Type != memberType)
                valueExpression = Convert(valueExpression, memberType);

            expressions.Add(
                Assign(MakeMemberAccess(instanceVariable, memberInfo), valueExpression));
        }

        if (includeNavigations)
        {
            foreach (var navigation in entityType.GetNavigations())
            {
                if (!navigation.TargetEntityType.IsOwned()
                    || navigation.PropertyInfo is null
                    || !navigation.PropertyInfo.CanWrite)
                    continue;

                var navigationValueExpression = navigation.IsCollection
                    ? CreateOwnedCollectionMaterializationExpression(navigation, instanceVariable)
                    : CreateOwnedReferenceMaterializationExpression(navigation);

                var memberExpression = Property(instanceVariable, navigation.PropertyInfo);
                if (navigationValueExpression.Type != memberExpression.Type)
                    navigationValueExpression =
                        Convert(navigationValueExpression, memberExpression.Type);

                expressions.Add(Assign(memberExpression, navigationValueExpression));
            }
        }

        expressions.Add(instanceVariable);
        return Block([instanceVariable], expressions);
    }

    /// <summary>
    ///     Builds an expression that reads an owned collection from a list attribute (
    ///     <see cref="AttributeValue.L" />) and validates null/missing shape semantics.
    /// </summary>
    private static Expression CreateReadOwnedListExpression(
        Expression parentMapExpression,
        string containingAttributeName,
        bool required,
        string navigationPath)
    {
        var attributeValueVariable = Variable(typeof(AttributeValue), "ownedCollectionAv");

        var tryGetValueExpression = Call(
            parentMapExpression,
            DictionaryTryGetValueMethod,
            Constant(containingAttributeName),
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
                    Constant($"Required owned collection '{navigationPath}' is missing or NULL.")),
                typeof(List<AttributeValue>))
            : Constant(null, typeof(List<AttributeValue>));

        Expression wrongShapeExpression = Throw(
            New(
                InvalidOperationExceptionCtor,
                Constant($"Owned collection '{navigationPath}' attribute is not a list (L).")),
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

        if (!DynamoTypeMappingSource.TryGetListElementType(targetType, out var elementType))
            return expression.Type != targetType ? Convert(expression, targetType) : expression;

        var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
        var enumerableExpression = expression;
        if (!enumerableType.IsAssignableFrom(enumerableExpression.Type))
            enumerableExpression = Convert(enumerableExpression, enumerableType);

        if (targetType.IsArray)
        {
            var toArrayMethod = EnumerableMethods.ToArray.MakeGenericMethod(elementType);
            var arrayExpression = Call(toArrayMethod, enumerableExpression);
            return arrayExpression.Type == targetType
                ? arrayExpression
                : Convert(arrayExpression, targetType);
        }

        var toListMethod = EnumerableMethods.ToList.MakeGenericMethod(elementType);
        var listExpression = Call(toListMethod, enumerableExpression);
        return listExpression.Type == targetType
            ? listExpression
            : Convert(listExpression, targetType);
    }

    /// <summary>Creates and populates a navigation collection instance via EF Core's collection accessor.</summary>
    private static TCollection PopulateCollection<TEntity, TCollection>(
        IClrCollectionAccessor accessor,
        IEnumerable<TEntity> entities)
    {
        var collection = (ICollection<TEntity>)accessor.Create();
        foreach (var entity in entities)
            collection.Add(entity);

        return (TCollection)collection;
    }

    /// <summary>
    ///     Populates an existing owner collection instance (or creates one) via EF Core's collection
    ///     accessor.
    /// </summary>
    private static TCollection PopulateCollectionOnOwner<TEntity, TCollection>(
        IClrCollectionAccessor accessor,
        object owner,
        IEnumerable<TEntity> entities)
    {
        var collection = (ICollection<TEntity>)accessor.GetOrCreate(owner, true);
        collection.Clear();
        foreach (var entity in entities)
            collection.Add(entity);

        return (TCollection)collection;
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

                // Get type mapping for converter support
                var typeMapping = property.GetTypeMapping();

                // Strict requiredness, aligned with relational and Mongo providers.
                var required = !property.IsNullable;
                var entityTypeDisplayName = property.DeclaringType.DisplayName();

                // Build inline expression: item.TryGetValue(...) ? value : default
                var valueExpression = CreateGetValueExpression(
                    property.Name,
                    targetType,
                    typeMapping,
                    required,
                    entityTypeDisplayName,
                    property);

                if (property.IsOwnedOrdinalKeyProperty())
                {
                    if (_ordinalParameterBindings.TryGetValue(
                        _attributeContextStack.Peek(),
                        out var boundOrdinal))
                        valueExpression = boundOrdinal.Type == node.Type
                            ? boundOrdinal
                            : Convert(boundOrdinal, node.Type);
                }
                else if (TryCreateOwnedPrincipalKeyExpression(
                    property,
                    targetType,
                    out var ownerKeyExpression))
                {
                    valueExpression = ownerKeyExpression;
                }

                return valueExpression.Type != node.Type
                    ? Convert(valueExpression, node.Type)
                    : valueExpression;
            }
        }

        return base.VisitMethodCall(node);
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
        var converter = typeMapping?.Converter;

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

        var isNullFlagExpression = Equal(
            Property(attributeValueVariable, AttributeValueNullProperty),
            Constant(true, typeof(bool?)));

        var isDynamoNullExpression = OrElse(isAttributeValueNullExpression, isNullFlagExpression);

        Expression valueExpression;
        var isCollectionType = DynamoTypeMappingSource.TryGetDictionaryValueType(type, out _, out _)
            || DynamoTypeMappingSource.TryGetSetElementType(type, out _)
            || DynamoTypeMappingSource.TryGetListElementType(type, out _);

        if (isCollectionType)
        {
            valueExpression = CreateCollectionValueExpression(
                attributeValueVariable,
                type,
                typeMapping,
                propertyPath,
                required,
                property);
        }
        else
        {
            // Extract wire primitive: attributeValue.S, long.Parse(attributeValue.N), etc.
            var primitiveType = converter?.ProviderClrType ?? type;
            var isNullablePrimitive = Nullable.GetUnderlyingType(primitiveType) != null;
            var wireType = Nullable.GetUnderlyingType(primitiveType) ?? primitiveType;
            if (!IsWirePrimitiveType(wireType))
                throw new InvalidOperationException(
                    $"Cannot materialize property '{propertyPath}' as CLR type '{type.FullName ?? type.Name}'. "
                    + $"The effective wire type '{wireType.FullName ?? wireType.Name}' is not a supported DynamoDB primitive. "
                    + $"Required={required}, HasTypeMapping={typeMapping != null}. "
                    + "Use an owned entity type for embedded complex objects, or configure a value converter/type mapping to a supported DynamoDB primitive wire type.");

            var primitiveExpression = CreateAttributeValueToPrimitiveExpression(
                attributeValueVariable,
                wireType,
                isNullablePrimitive);

            if (primitiveExpression.Type != primitiveType)
                primitiveExpression = Convert(primitiveExpression, primitiveType);

            // Inline converter: DateTime.Parse(...), Guid.Parse(...), (int)long.Parse(...),
            // etc.
            valueExpression = primitiveExpression;
            if (converter != null)
                valueExpression = ReplacingExpressionVisitor.Replace(
                    converter.ConvertFromProviderExpression.Parameters.Single(),
                    primitiveExpression,
                    converter.ConvertFromProviderExpression.Body);

            if (valueExpression.Type != type)
                valueExpression = Convert(valueExpression, type);

            var expectedWireMember = GetExpectedWireMemberName(wireType);
            var missingWireValueReturnExpression = required
                ? CreateThrow(
                    $"Required property '{propertyPath}' did not contain a value for expected DynamoDB wire member '{expectedWireMember}'.")
                : Default(type);

            // Ensure we never parse/convert when the expected wire member isn't present (e.g. N ==
            // null).
            // This also makes explicit DynamoDB NULL behave like store null and prevents
            // long.Parse(null).
            if (TryCreateHasWireValueExpression(
                attributeValueVariable,
                wireType,
                out var hasWireValueExpression))
                valueExpression = Condition(
                    hasWireValueExpression,
                    valueExpression,
                    missingWireValueReturnExpression);
        }

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
    ///     Creates an expression that reads an owned dependent key property from the owner context
    ///     when the dependent key is propagated from the principal key.
    /// </summary>
    /// <remarks>
    ///     For deeply-nested owned entities the FK chain may span multiple ownership levels (e.g.
    ///     <c>OrderLine.OwnedShapeItemPk → Order.OwnedShapeItemPk → OwnedShapeItem.Pk</c>). This method
    ///     follows the chain through successive entries of <see cref="_ownerAttributeContextStack" />
    ///     until it reaches a non-shadow principal, or an ordinal key property that must be resolved from
    ///     the parent collection's ordinal binding.
    /// </remarks>
    private bool TryCreateOwnedPrincipalKeyExpression(
        IProperty property,
        Type targetType,
        out Expression valueExpression)
    {
        valueExpression = default!;

        if (property.DeclaringType is not IEntityType declaringEntityType)
            return false;

        var ownership = declaringEntityType.FindOwnership();
        if (ownership == null || !ownership.Properties.Contains(property))
            return false;

        if (_ownerAttributeContextStack.Count == 0)
            return false;

        // Stack.ToArray() returns elements in LIFO order: index 0 = top = immediate owner.
        var ownerContexts = _ownerAttributeContextStack.ToArray();

        // Walk the principal chain. Each hop through a shadow FK on an owned entity
        // corresponds to moving one level deeper (further from the leaf) in the owner stack.
        var depth = 0;
        var principalProperty = property.FindFirstPrincipal();
        while (principalProperty != null
            && principalProperty.IsShadowProperty()
            && principalProperty.DeclaringType is IEntityType principalEntityType
            && principalEntityType.FindOwnership() is { } principalOwnership
            && principalOwnership.Properties.Contains(principalProperty)
            && depth + 1 < ownerContexts.Length)
        {
            principalProperty = principalProperty.FindFirstPrincipal();
            depth++;
        }

        if (principalProperty == null)
            return false;

        var ownerContext = ownerContexts[Math.Min(depth, ownerContexts.Length - 1)];

        // If the resolved principal is an ordinal key on an owned entity it is not stored as a
        // DynamoDB attribute — its value lives in the parent collection's ordinal binding.
        if (principalProperty.IsOwnedOrdinalKeyProperty()
            && _ordinalParameterBindings.TryGetValue(ownerContext, out var ordinalBinding))
        {
            valueExpression = ordinalBinding.Type == targetType
                ? ordinalBinding
                : Convert(ordinalBinding, targetType);
            return true;
        }

        valueExpression = CreateGetValueExpression(
            principalProperty.Name,
            targetType,
            principalProperty.GetTypeMapping(),
            !principalProperty.IsNullable,
            principalProperty.DeclaringType.DisplayName(),
            principalProperty,
            ownerContext);

        return true;
    }

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
    ///         <item>long → long.Parse(attributeValue.N)</item>
    ///         <item>double → double.Parse(attributeValue.N)</item>
    ///         <item>decimal → decimal.Parse(attributeValue.N)</item>
    ///         <item>byte[] → attributeValue.B?.ToArray()</item>
    ///     </list>
    ///     Non-primitive types (Guid,
    ///     DateTimeOffset, etc.) are handled by EF Core value converters.
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
            + $"Supported types: string, bool, numeric types (int, long, float, double, decimal, etc.), byte[]");
    }

    /// <summary>Checks whether a CLR type is a non-nullable value type.</summary>
    private static bool IsNonNullableValueType(Type type)
        => type.IsValueType && Nullable.GetUnderlyingType(type) == null;

    /// <summary>
    ///     Returns the expected primitive <see cref="AttributeValue" /> wire member for a wire CLR
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
    ///     Builds a typed conversion expression from <see cref="AttributeValue" /> to a model CLR
    ///     value.
    /// </summary>
    private static Expression CreateTypedValueExpressionFromAttributeValue(
        Expression attributeValueExpression,
        Type modelType,
        CoreTypeMapping? typeMapping,
        string propertyPath,
        bool required)
    {
        var converter = typeMapping?.Converter;
        var providerType = converter?.ProviderClrType ?? modelType;
        var wireType = Nullable.GetUnderlyingType(providerType) ?? providerType;
        var allowNullBool = Nullable.GetUnderlyingType(providerType) != null;

        var providerValueExpression = CreateAttributeValueToPrimitiveExpression(
            attributeValueExpression,
            wireType,
            allowNullBool);

        if (providerValueExpression.Type != providerType)
            providerValueExpression = Convert(providerValueExpression, providerType);

        var modelValueExpression = providerValueExpression;
        if (converter != null)
            modelValueExpression = ReplacingExpressionVisitor.Replace(
                converter.ConvertFromProviderExpression.Parameters.Single(),
                providerValueExpression,
                converter.ConvertFromProviderExpression.Body);

        if (modelValueExpression.Type != modelType)
            modelValueExpression = Convert(modelValueExpression, modelType);

        Expression missingWireValueExpression = required
            ? Throw(
                New(
                    InvalidOperationExceptionCtor,
                    Constant(
                        $"Required property '{propertyPath}' did not contain a value for expected DynamoDB wire member '{GetExpectedWireMemberName(wireType)}'.")),
                modelType)
            : Default(modelType);

        if (TryCreateHasWireValueExpression(
            attributeValueExpression,
            wireType,
            out var hasWireValueExpression))
            modelValueExpression = Condition(
                hasWireValueExpression,
                modelValueExpression,
                missingWireValueExpression);

        var isAttributeValueNullExpression = Equal(
            attributeValueExpression,
            Constant(null, typeof(AttributeValue)));
        var isNullFlagExpression = Equal(
            Property(attributeValueExpression, AttributeValueNullProperty),
            Constant(true, typeof(bool?)));
        var isDynamoNullExpression = OrElse(isAttributeValueNullExpression, isNullFlagExpression);

        Expression nullReturnExpression = required
            ? Throw(
                New(
                    InvalidOperationExceptionCtor,
                    Constant($"Required property '{propertyPath}' was set to DynamoDB NULL.")),
                modelType)
            : Default(modelType);

        return Condition(isDynamoNullExpression, nullReturnExpression, modelValueExpression);
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

    /// <summary>Finds an owned-collection element shaper expression nested within an expression tree.</summary>
    private static Expression? TryExtractElementShaperExpression(
        Expression? expression,
        IEntityType targetEntityType)
    {
        if (expression == null)
            return null;

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

    /// <summary>Finds an element shaper for a specific collection navigation inside a source shaper tree.</summary>
    private static Expression? TryExtractCollectionNavigationElementShaperExpression(
        Expression? expression,
        INavigation navigation)
    {
        if (expression == null)
            return null;

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
    ///     A copy of this class also lives in <see cref="DynamoProjectionBindingExpressionVisitor" />
    ///     ; the duplication is deliberate to avoid cross-visitor coupling.
    /// </remarks>
    private sealed class ElementShaperExpressionFinder : ExpressionVisitor
    {
        private readonly IEntityType _targetEntityType;

        /// <summary>Creates a finder scoped to a specific target entity type.</summary>
        public ElementShaperExpressionFinder(IEntityType targetEntityType)
            => _targetEntityType = targetEntityType;

        /// <summary>Gets the first collection element shaper expression found during traversal.</summary>
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

    /// <summary>Finds a specific nested collection navigation shaper within a parent shaper expression.</summary>
    private sealed class NavigationCollectionShaperFinder(INavigation navigation)
        : ExpressionVisitor
    {
        /// <summary>Gets the discovered element shaper expression for the requested navigation.</summary>
        public Expression? Result { get; private set; }

        /// <summary>Visits nodes until the requested collection navigation shaper is found.</summary>
        public override Expression? Visit(Expression? node)
        {
            if (node == null || Result != null)
                return node;

            // In the removal phase the caller needs the concrete element shaper so it can be
            // passed directly to CreateOwnedCollectionMaterializationExpression. Unlike the
            // companion finder in DynamoProjectionBindingExpressionVisitor — which returns the
            // full ShapedQueryExpression.ShaperExpression — we drill one level deeper here via
            // TryExtractElementShaperExpression to extract the actual element shaper.
            if (
                node is MaterializeCollectionNavigationExpression
                    materializeCollectionNavigationExpression
                && materializeCollectionNavigationExpression.Navigation is INavigation
                    candidateNavigation
                && candidateNavigation.Name == navigation.Name
                && candidateNavigation.TargetEntityType.ClrType
                == navigation.TargetEntityType.ClrType)
            {
                Result = TryExtractElementShaperExpression(
                    materializeCollectionNavigationExpression.Subquery,
                    navigation.TargetEntityType);
                return node;
            }

            if (node is DynamoCollectionShaperExpression
                {
                    Navigation: INavigation collectionNavigation, InnerShaper: var innerShaper,
                }
                && collectionNavigation.Name == navigation.Name
                && collectionNavigation.TargetEntityType.ClrType
                == navigation.TargetEntityType.ClrType)
            {
                Result = innerShaper;
                return node;
            }

            return base.Visit(node);
        }
    }

    /// <summary>Finds whether a target structural shaper exists within a tree.</summary>
    /// <remarks>
    ///     A copy of this class also lives in <see cref="DynamoProjectionBindingExpressionVisitor" />
    ///     ; the duplication is deliberate to avoid cross-visitor coupling.
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
}
