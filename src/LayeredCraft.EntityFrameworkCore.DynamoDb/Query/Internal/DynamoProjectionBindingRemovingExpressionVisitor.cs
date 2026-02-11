using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using static System.Linq.Expressions.Expression;
using Convert = System.Convert;

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

    private static readonly ConstructorInfo InvalidOperationExceptionCtor =
        typeof(InvalidOperationException).GetConstructor([typeof(string)])!;

    private static readonly MethodInfo DictionaryTryGetValueMethod =
        typeof(Dictionary<string, AttributeValue>).GetMethod(nameof(Dictionary<,>.TryGetValue))!;

    private static readonly MethodInfo LongParseMethod =
        typeof(long).GetMethod(nameof(long.Parse), [typeof(string), typeof(IFormatProvider)])!;

    private static readonly MethodInfo DoubleParseMethod =
        typeof(double).GetMethod(nameof(double.Parse), [typeof(string), typeof(IFormatProvider)])!;

    private static readonly MethodInfo DecimalParseMethod =
        typeof(decimal).GetMethod(
            nameof(decimal.Parse),
            [typeof(string), typeof(IFormatProvider)])!;

    private static readonly MethodInfo MemoryStreamToArrayMethod =
        typeof(MemoryStream).GetMethod(nameof(MemoryStream.ToArray))!;

    private static readonly MethodInfo ConvertAttributeValueToClrValueMethod =
        typeof(DynamoProjectionBindingRemovingExpressionVisitor).GetMethod(
            nameof(ConvertAttributeValueToClrValue),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly Type[] SupportedListInterfaces =
    [
        typeof(IList<>), typeof(IReadOnlyList<>), typeof(IEnumerable<>),
    ];

    private static readonly Type[] SupportedDictionaryInterfaces =
    [
        typeof(IDictionary<,>), typeof(IReadOnlyDictionary<,>),
    ];

    private static readonly Type[]
        SupportedSetInterfaces = [typeof(ISet<>), typeof(IReadOnlySet<>)];

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
                    null);
            }
        }

        return base.VisitExtension(node);
    }

    /// <summary>
    ///     Intercepts ValueBufferTryReadValue calls and replaces them with inline expression trees
    ///     that extract values from Dictionary&lt;string, AttributeValue&gt; with zero boxing overhead.
    /// </summary>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.TryGetEFPropertyArguments(out _, out var propertyName)
            && !string.IsNullOrEmpty(propertyName))
            return CreateGetValueExpression(
                itemParameter,
                propertyName!,
                node.Type,
                null,
                false,
                null);

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
                    entityTypeDisplayName);

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
    private static BlockExpression CreateGetValueExpression(
        ParameterExpression itemParameter,
        string propertyName,
        Type type,
        CoreTypeMapping? typeMapping,
        bool required,
        string? entityTypeDisplayName)
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

        if (IsCollectionType(type))
        {
            var collectionValueExpression = Convert(
                Call(
                    ConvertAttributeValueToClrValueMethod,
                    attributeValueVariable,
                    Constant(type, typeof(Type)),
                    Constant(typeMapping, typeof(CoreTypeMapping)),
                    Constant(propertyPath),
                    Constant(required)),
                type);

            var completeCollectionExpression = Condition(
                tryGetValueExpression,
                Condition(isDynamoNullExpression, nullReturnExpression, collectionValueExpression),
                missingReturnExpression);

            return Block([attributeValueVariable], completeCollectionExpression);
        }

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

        // Inline converter: DateTime.Parse(...), Guid.Parse(...), (int)long.Parse(...), etc.
        var valueExpression = primitiveExpression;
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
        var cultureInfo = Constant(CultureInfo.InvariantCulture);

        // Numeric types: parse as long/double and convert to target type
        if (primitiveType == typeof(short))
            return Convert(Call(LongParseMethod, nProperty, cultureInfo), typeof(short));

        if (primitiveType == typeof(byte))
            return Convert(Call(LongParseMethod, nProperty, cultureInfo), typeof(byte));

        if (primitiveType == typeof(int))
            return Convert(Call(LongParseMethod, nProperty, cultureInfo), typeof(int));

        if (primitiveType == typeof(long))
            return Call(LongParseMethod, nProperty, cultureInfo);

        if (primitiveType == typeof(float))
            return Convert(Call(DoubleParseMethod, nProperty, cultureInfo), typeof(float));

        if (primitiveType == typeof(double))
            return Call(DoubleParseMethod, nProperty, cultureInfo);

        if (primitiveType == typeof(decimal))
            return Call(DecimalParseMethod, nProperty, cultureInfo);

        throw new InvalidOperationException(
            $"Cannot create expression for AttributeValue to primitive type '{primitiveType.Name}'. "
            + $"Supported types: string, bool, numeric types (int, long, float, double, decimal, etc.), byte[]");
    }

    private static bool IsNonNullableValueType(Type type)
        => type.IsValueType && Nullable.GetUnderlyingType(type) == null;

    private static string GetExpectedWireMemberName(Type wireType)
        => wireType == typeof(string) ? nameof(AttributeValue.S) :
            wireType == typeof(bool) ? nameof(AttributeValue.BOOL) :
            wireType == typeof(byte[]) ? nameof(AttributeValue.B) : nameof(AttributeValue.N);

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

    private static bool IsCollectionType(Type type)
    {
        if (type == typeof(string) || type == typeof(byte[]))
            return false;

        if (type.IsArray)
            return true;

        if (!type.IsGenericType)
            return false;

        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(List<>)
            || definition == typeof(IList<>)
            || definition == typeof(IReadOnlyList<>)
            || definition == typeof(Dictionary<,>)
            || definition == typeof(IDictionary<,>)
            || definition == typeof(IReadOnlyDictionary<,>)
            || definition == typeof(ReadOnlyDictionary<,>)
            || definition == typeof(HashSet<>)
            || definition == typeof(ISet<>);
    }

    private static object? ConvertAttributeValueToClrValue(
        AttributeValue attributeValue,
        Type targetType,
        CoreTypeMapping? typeMapping,
        string propertyPath,
        bool required)
    {
        if (attributeValue == null || attributeValue.NULL == true)
        {
            if (required)
                throw new InvalidOperationException(
                    $"Required property '{propertyPath}' was set to DynamoDB NULL.");

            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (TryGetDictionaryValueType(targetType, out var valueType, out var isReadOnlyDictionary))
        {
            if (attributeValue.M == null)
                return HandleMissingWireValue(
                    targetType,
                    propertyPath,
                    required,
                    nameof(AttributeValue.M));

            var valueMapping = typeMapping?.ElementTypeMapping;
            var dictionary =
                CreateMutableDictionaryInstance(targetType, valueType, attributeValue.M.Count);

            foreach (var (key, mapValue) in attributeValue.M)
                dictionary[key] = ConvertAttributeValueToClrValue(
                    mapValue,
                    valueType,
                    valueMapping,
                    $"{propertyPath}.{key}",
                    false)!;

            if (!isReadOnlyDictionary)
                return dictionary;

            var readOnlyDictionaryType =
                typeof(ReadOnlyDictionary<,>).MakeGenericType(typeof(string), valueType);
            return Activator.CreateInstance(readOnlyDictionaryType, dictionary)!;
        }

        if (TryGetSetElementType(targetType, out var setElementType))
        {
            var elementMapping = typeMapping?.ElementTypeMapping;
            var set = CreateSetInstance(targetType, setElementType);
            var addMethod = set.GetType().GetMethod(nameof(HashSet<int>.Add))!;

            if (attributeValue.SS != null)
            {
                foreach (var setElement in attributeValue.SS)
                    addMethod.Invoke(
                        set,
                        [
                            ConvertProviderValueToModelValue(
                                setElement,
                                setElementType,
                                elementMapping),
                        ]);

                return set;
            }

            if (attributeValue.NS != null)
            {
                foreach (var setElement in attributeValue.NS)
                {
                    var providerType = GetProviderType(setElementType, elementMapping);
                    var parsed = ParseNumericString(setElement, providerType);
                    addMethod.Invoke(
                        set,
                        [ConvertProviderValueToModelValue(parsed, setElementType, elementMapping)]);
                }

                return set;
            }

            if (attributeValue.BS != null)
            {
                foreach (var setElement in attributeValue.BS)
                    addMethod.Invoke(
                        set,
                        [
                            ConvertProviderValueToModelValue(
                                setElement?.ToArray(),
                                setElementType,
                                elementMapping),
                        ]);

                return set;
            }

            return HandleMissingWireValue(targetType, propertyPath, required, "SS/NS/BS");
        }

        if (TryGetListElementType(targetType, out var elementType))
        {
            if (attributeValue.L == null)
                return HandleMissingWireValue(
                    targetType,
                    propertyPath,
                    required,
                    nameof(AttributeValue.L));

            var elementMapping = typeMapping?.ElementTypeMapping;
            var list = CreateListInstance(targetType, elementType, attributeValue.L.Count);

            foreach (var listElement in attributeValue.L)
                list.Add(
                    ConvertAttributeValueToClrValue(
                        listElement,
                        elementType,
                        elementMapping,
                        propertyPath,
                        false));

            if (targetType.IsArray)
            {
                var array = Array.CreateInstance(elementType, list.Count);
                list.CopyTo(array, 0);
                return array;
            }

            return list;
        }

        return ConvertScalarAttributeValue(
            attributeValue,
            targetType,
            typeMapping,
            propertyPath,
            required);
    }

    private static object? ConvertScalarAttributeValue(
        AttributeValue attributeValue,
        Type targetType,
        CoreTypeMapping? typeMapping,
        string propertyPath,
        bool required)
    {
        var converter = typeMapping?.Converter;
        var providerType = GetProviderType(targetType, typeMapping);

        var providerValue =
            providerType == typeof(string) ? attributeValue.S :
            providerType == typeof(bool) ? attributeValue.BOOL :
            providerType == typeof(byte[]) ? attributeValue.B?.ToArray() :
            ParseNumericString(attributeValue.N, providerType);

        if (providerValue == null)
            return HandleMissingWireValue(
                targetType,
                propertyPath,
                required,
                GetExpectedWireMemberName(providerType));

        var modelValue = converter == null
            ? providerValue
            : converter.ConvertFromProvider(providerValue);

        if (modelValue == null)
            return required ? throw new InvalidOperationException(
                    $"Required property '{propertyPath}' did not contain a value for expected DynamoDB wire member '{GetExpectedWireMemberName(providerType)}'.") :
                targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        var nonNullableTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (nonNullableTargetType.IsAssignableFrom(modelValue.GetType()))
            return modelValue;

        return Convert.ChangeType(modelValue, nonNullableTargetType, CultureInfo.InvariantCulture);
    }

    private static Type GetProviderType(Type targetType, CoreTypeMapping? typeMapping)
    {
        var providerType = typeMapping?.Converter?.ProviderClrType ?? targetType;
        return Nullable.GetUnderlyingType(providerType) ?? providerType;
    }

    private static object? HandleMissingWireValue(
        Type targetType,
        string propertyPath,
        bool required,
        string wireMemberName)
    {
        if (required)
            throw new InvalidOperationException(
                $"Required property '{propertyPath}' did not contain a value for expected DynamoDB wire member '{wireMemberName}'.");

        return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
    }

    private static object? ConvertProviderValueToModelValue(
        object? providerValue,
        Type targetType,
        CoreTypeMapping? typeMapping)
    {
        var converter = typeMapping?.Converter;
        if (converter != null)
            return converter.ConvertFromProvider(providerValue);

        if (providerValue == null)
            return null;

        var nonNullableTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return nonNullableTargetType.IsAssignableFrom(providerValue.GetType())
            ? providerValue
            : Convert.ChangeType(
                providerValue,
                nonNullableTargetType,
                CultureInfo.InvariantCulture);
    }

    private static object ParseNumericString(string? value, Type numericType)
    {
        if (value == null)
            throw new InvalidOperationException("Numeric DynamoDB wire value was missing.");

        if (numericType == typeof(byte))
            return byte.Parse(value, CultureInfo.InvariantCulture);

        if (numericType == typeof(short))
            return short.Parse(value, CultureInfo.InvariantCulture);

        if (numericType == typeof(int))
            return int.Parse(value, CultureInfo.InvariantCulture);

        if (numericType == typeof(long))
            return long.Parse(value, CultureInfo.InvariantCulture);

        if (numericType == typeof(float))
            return float.Parse(value, CultureInfo.InvariantCulture);

        if (numericType == typeof(double))
            return double.Parse(value, CultureInfo.InvariantCulture);

        if (numericType == typeof(decimal))
            return decimal.Parse(value, CultureInfo.InvariantCulture);

        throw new InvalidOperationException($"Unsupported numeric provider type '{numericType}'.");
    }

    private static bool TryGetListElementType(Type clrType, out Type elementType)
    {
        elementType = null!;
        if (clrType == typeof(string))
            return false;

        if (clrType == typeof(byte[]))
            return false;

        if (clrType.IsArray)
        {
            var arrayElementType = clrType.GetElementType();
            if (arrayElementType == null)
                return false;

            elementType = arrayElementType;
            return true;
        }

        var listInterface = TryGetGenericTypeFromSelfOrInterfaces(clrType, SupportedListInterfaces);
        if (listInterface == null)
            return false;

        elementType = listInterface.GetGenericArguments()[0];
        return true;
    }

    private static bool TryGetDictionaryValueType(
        Type clrType,
        out Type valueType,
        out bool isReadOnlyDictionary)
    {
        valueType = null!;
        isReadOnlyDictionary = false;
        if (clrType.IsGenericType
            && clrType.GetGenericTypeDefinition() == typeof(ReadOnlyDictionary<,>))
            isReadOnlyDictionary = true;

        var dictionaryInterface =
            TryGetGenericTypeFromSelfOrInterfaces(clrType, SupportedDictionaryInterfaces);

        if (dictionaryInterface == null)
            return false;

        var genericArguments = dictionaryInterface.GetGenericArguments();
        if (genericArguments[0] != typeof(string))
            return false;

        valueType = genericArguments[1];
        return true;
    }

    private static bool TryGetSetElementType(Type clrType, out Type elementType)
    {
        elementType = null!;
        var setInterface = TryGetGenericTypeFromSelfOrInterfaces(clrType, SupportedSetInterfaces);
        if (setInterface == null)
            return false;

        elementType = setInterface.GetGenericArguments()[0];
        return true;
    }

    private static Type? TryGetGenericTypeFromSelfOrInterfaces(
        Type clrType,
        IReadOnlyList<Type> supportedOpenGenericTypes)
    {
        if (clrType.IsGenericType)
        {
            var genericTypeDefinition = clrType.GetGenericTypeDefinition();
            foreach (var openGenericType in supportedOpenGenericTypes)
                if (genericTypeDefinition == openGenericType)
                    return clrType;
        }

        foreach (var implementedInterface in clrType.GetInterfaces())
        {
            if (!implementedInterface.IsGenericType)
                continue;

            var interfaceDefinition = implementedInterface.GetGenericTypeDefinition();
            foreach (var openGenericType in supportedOpenGenericTypes)
                if (interfaceDefinition == openGenericType)
                    return implementedInterface;
        }

        return null;
    }

    private static IDictionary CreateMutableDictionaryInstance(
        Type targetType,
        Type valueType,
        int capacity)
    {
        if (!targetType.IsAbstract
            && !targetType.IsInterface
            && ImplementsGenericInterface(targetType, typeof(IDictionary<,>))
            && targetType.GetConstructor(Type.EmptyTypes) is { IsPublic: true })
            return (IDictionary)Activator.CreateInstance(targetType)!;

        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
        return (IDictionary)Activator.CreateInstance(dictionaryType, capacity)!;
    }

    private static IList CreateListInstance(Type targetType, Type elementType, int capacity)
    {
        if (!targetType.IsAbstract
            && !targetType.IsInterface
            && ImplementsGenericInterface(targetType, typeof(IList<>))
            && targetType.GetConstructor(Type.EmptyTypes) is { IsPublic: true })
            return (IList)Activator.CreateInstance(targetType)!;

        var listType = typeof(List<>).MakeGenericType(elementType);
        return (IList)Activator.CreateInstance(listType, capacity)!;
    }

    private static object CreateSetInstance(Type targetType, Type elementType)
    {
        if (!targetType.IsAbstract
            && !targetType.IsInterface
            && ImplementsGenericInterface(targetType, typeof(ISet<>))
            && targetType.GetConstructor(Type.EmptyTypes) is { IsPublic: true })
            return Activator.CreateInstance(targetType)!;

        return Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(elementType))!;
    }

    private static bool ImplementsGenericInterface(Type type, Type openGenericInterface)
    {
        foreach (var implementedInterface in type.GetInterfaces())
            if (implementedInterface.IsGenericType
                && implementedInterface.GetGenericTypeDefinition() == openGenericInterface)
                return true;

        return false;
    }
}
