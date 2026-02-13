using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using DynamoDocument = Amazon.DynamoDBv2.DocumentModel.Document;
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

    private static readonly MethodInfo EnumerableToListMethodDefinition = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method => method.Name == nameof(Enumerable.ToList)
            && method.IsGenericMethodDefinition
            && method.GetParameters().Length == 1);

    private static readonly MethodInfo EnumerableToArrayMethodDefinition = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method => method.Name == nameof(Enumerable.ToArray)
            && method.IsGenericMethodDefinition
            && method.GetParameters().Length == 1);

    private static readonly MethodInfo DeserializeComplexAttributeValueMethod =
        typeof(DynamoProjectionBindingRemovingExpressionVisitor).GetMethod(
            nameof(DeserializeComplexAttributeValue),
            BindingFlags.Static | BindingFlags.NonPublic)!;

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

    /// <summary>
    ///     Handles ProjectionBindingExpression for custom Select projections. Converts member-based
    ///     bindings to indexed dictionary access and supports index-based bindings.
    /// </summary>
    protected override Expression VisitExtension(Expression node)
    {
        if (node is MaterializeCollectionNavigationExpression
            materializeCollectionNavigationExpression)
        {
            var navigation = materializeCollectionNavigationExpression.Navigation;
            var navigationExpression = CreateGetValueExpression(
                itemParameter,
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
            var navigationExpression = CreateGetValueExpression(
                itemParameter,
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

                // Use unified code path with converter support
                return CreateGetValueExpression(
                    itemParameter,
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
                    itemParameter,
                    propertyName,
                    projectionBinding.Type,
                    typeMapping,
                    required,
                    null,
                    null);
            }
        }

        return base.VisitExtension(node);
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
            var toArrayMethod = EnumerableToArrayMethodDefinition.MakeGenericMethod(elementType);
            var arrayExpression = Call(toArrayMethod, enumerableExpression);
            return arrayExpression.Type == targetType
                ? arrayExpression
                : Convert(arrayExpression, targetType);
        }

        var toListMethod = EnumerableToListMethodDefinition.MakeGenericMethod(elementType);
        var listExpression = Call(toListMethod, enumerableExpression);
        return listExpression.Type == targetType
            ? listExpression
            : Convert(listExpression, targetType);
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
                    itemParameter,
                    property.Name,
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
    ///     Rewrites member access with owned-complex null-propagation when the receiver is complex
    ///     materialization.
    /// </summary>
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
            || !ContainsMethodCall(instanceExpression, DeserializeComplexAttributeValueMethod))
            return memberExpression;

        return Condition(
            Equal(instanceExpression, Constant(null, instanceExpression.Type)),
            Default(node.Type),
            memberExpression);
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
    private static BlockExpression CreateGetValueExpression(
        ParameterExpression itemParameter,
        string propertyName,
        Type type,
        CoreTypeMapping? typeMapping,
        bool required,
        string? entityTypeDisplayName,
        IProperty? property)
    {
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

        if (ShouldUseComplexDeserialization(type, typeMapping, property))
        {
            valueExpression = CreateComplexDeserializationExpression(attributeValueVariable, type);
        }
        else
        {
            if (isCollectionType)
                valueExpression = CreateCollectionValueExpression(
                    attributeValueVariable,
                    type,
                    typeMapping,
                    propertyPath,
                    required,
                    property);
            else
            {
                // Extract wire primitive: attributeValue.S, long.Parse(attributeValue.N), etc.
                var primitiveType = converter?.ProviderClrType ?? type;
                var isNullablePrimitive = Nullable.GetUnderlyingType(primitiveType) != null;
                var wireType = Nullable.GetUnderlyingType(primitiveType) ?? primitiveType;
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

                // Ensure we never parse/convert when the expected wire member isn't present (e.g. N
                // ==
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

    /// <summary>Returns true when the value should be materialized as a complex embedded document/list.</summary>
    private static bool ShouldUseComplexDeserialization(
        Type targetType,
        CoreTypeMapping? typeMapping,
        IProperty? property)
    {
        if (typeMapping != null)
            return false;

        if (DynamoTypeMappingSource.TryGetListElementType(targetType, out var listElementType))
        {
            var elementMapping = property?.GetElementType()?.GetTypeMapping();
            return elementMapping == null && !IsWirePrimitiveType(listElementType);
        }

        if (DynamoTypeMappingSource.TryGetSetElementType(targetType, out var setElementType))
        {
            var elementMapping = property?.GetElementType()?.GetTypeMapping();
            return elementMapping == null && !IsWirePrimitiveType(setElementType);
        }

        if (DynamoTypeMappingSource.TryGetDictionaryValueType(targetType, out var valueType, out _))
        {
            var valueMapping = property?.GetElementType()?.GetTypeMapping();
            return valueMapping == null && !IsWirePrimitiveType(valueType);
        }

        return !IsWirePrimitiveType(targetType);
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

    /// <summary>Builds expression that deserializes a complex value from an AttributeValue tree via JSON.</summary>
    private static Expression CreateComplexDeserializationExpression(
        Expression attributeValueExpression,
        Type targetType)
    {
        var deserializeCall = Call(
            DeserializeComplexAttributeValueMethod,
            attributeValueExpression,
            Constant(targetType, typeof(Type)));

        return targetType.IsValueType
            ? Unbox(deserializeCall, targetType)
            : Convert(deserializeCall, targetType);
    }

    /// <summary>Deserializes an AttributeValue map/list tree into the requested CLR type.</summary>
    private static object? DeserializeComplexAttributeValue(
        AttributeValue attributeValue,
        Type targetType)
    {
        var wrapper =
            new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["value"] = attributeValue,
            };

        var document = DynamoDocument.FromAttributeMap(wrapper);
        using var jsonDocument = JsonDocument.Parse(document.ToJson());
        if (!jsonDocument.RootElement.TryGetProperty("value", out var valueElement))
            return null;

        return JsonSerializer.Deserialize(valueElement.GetRawText(), targetType);
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
            providerValueExpression = Property(wireListVariable, "Item", indexVariable);
        else
            providerValueExpression = CreateNumericStringParseExpression(
                Property(wireListVariable, "Item", indexVariable),
                nonNullableProviderType);

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

    /// <summary>Determines whether an expression tree contains a call to a specific method.</summary>
    private static bool ContainsMethodCall(Expression expression, MethodInfo methodInfo)
    {
        switch (expression)
        {
            case MethodCallExpression methodCall:
                if (methodCall.Method == methodInfo)
                    return true;

                if (methodCall.Object != null && ContainsMethodCall(methodCall.Object, methodInfo))
                    return true;

                foreach (var argument in methodCall.Arguments)
                    if (ContainsMethodCall(argument, methodInfo))
                        return true;

                return false;

            case BlockExpression blockExpression:
                foreach (var blockSubExpression in blockExpression.Expressions)
                    if (ContainsMethodCall(blockSubExpression, methodInfo))
                        return true;

                return false;

            case ConditionalExpression conditionalExpression:
                return ContainsMethodCall(conditionalExpression.Test, methodInfo)
                    || ContainsMethodCall(conditionalExpression.IfTrue, methodInfo)
                    || ContainsMethodCall(conditionalExpression.IfFalse, methodInfo);

            case BinaryExpression binaryExpression:
                return ContainsMethodCall(binaryExpression.Left, methodInfo)
                    || ContainsMethodCall(binaryExpression.Right, methodInfo)
                    || (binaryExpression.Conversion != null
                        && ContainsMethodCall(binaryExpression.Conversion, methodInfo));

            case UnaryExpression unaryExpression:
                return ContainsMethodCall(unaryExpression.Operand, methodInfo);

            case MemberExpression memberExpression:
                return memberExpression.Expression != null
                    && ContainsMethodCall(memberExpression.Expression, methodInfo);

            case NewExpression newExpression:
                foreach (var argument in newExpression.Arguments)
                    if (ContainsMethodCall(argument, methodInfo))
                        return true;

                return false;

            case NewArrayExpression newArrayExpression:
                foreach (var newArraySubExpression in newArrayExpression.Expressions)
                    if (ContainsMethodCall(newArraySubExpression, methodInfo))
                        return true;

                return false;

            case MemberInitExpression memberInitExpression:
                if (ContainsMethodCall(memberInitExpression.NewExpression, methodInfo))
                    return true;

                foreach (var binding in memberInitExpression.Bindings)
                    if (binding is MemberAssignment memberAssignment
                        && ContainsMethodCall(memberAssignment.Expression, methodInfo))
                        return true;

                return false;

            case LambdaExpression lambdaExpression:
                return ContainsMethodCall(lambdaExpression.Body, methodInfo);

            default:
                return false;
        }
    }
}
