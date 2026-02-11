using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
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

    private static readonly MethodInfo ConvertCollectionAttributeValueMethod =
        typeof(DynamoProjectionBindingRemovingExpressionVisitor).GetMethod(
            nameof(ConvertCollectionAttributeValue),
            BindingFlags.NonPublic | BindingFlags.Static)!;

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

        Expression valueExpression;
        var isCollectionType = DynamoTypeMappingSource.TryGetDictionaryValueType(type, out _, out _)
            || DynamoTypeMappingSource.TryGetSetElementType(type, out _)
            || DynamoTypeMappingSource.TryGetListElementType(type, out _);

        if (isCollectionType)
        {
            valueExpression = Convert(
                Call(
                    ConvertCollectionAttributeValueMethod,
                    attributeValueVariable,
                    Constant(type, typeof(Type)),
                    Constant(typeMapping, typeof(CoreTypeMapping)),
                    Constant(propertyPath),
                    Constant(required)),
                type);
        }
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

            // Inline converter: DateTime.Parse(...), Guid.Parse(...), (int)long.Parse(...), etc.
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

        if (primitiveType == typeof(ushort))
            return Convert(Call(LongParseMethod, nProperty, cultureInfo), typeof(ushort));

        if (primitiveType == typeof(sbyte))
            return Convert(Call(LongParseMethod, nProperty, cultureInfo), typeof(sbyte));

        if (primitiveType == typeof(byte))
            return Convert(Call(LongParseMethod, nProperty, cultureInfo), typeof(byte));

        if (primitiveType == typeof(int))
            return Convert(Call(LongParseMethod, nProperty, cultureInfo), typeof(int));

        if (primitiveType == typeof(uint))
            return Convert(Call(LongParseMethod, nProperty, cultureInfo), typeof(uint));

        if (primitiveType == typeof(long))
            return Call(LongParseMethod, nProperty, cultureInfo);

        if (primitiveType == typeof(ulong))
            return Convert(Call(DecimalParseMethod, nProperty, cultureInfo), typeof(ulong));

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

    /// <summary>Entry point used by compiled query expressions for collection materialization.</summary>
    private static object? ConvertCollectionAttributeValue(
        AttributeValue attributeValue,
        Type targetType,
        CoreTypeMapping? typeMapping,
        string propertyPath,
        bool required)
        => ConvertAttributeValueToClrValueCore(
            attributeValue,
            targetType,
            typeMapping,
            propertyPath,
            required);

    /// <summary>Converts an <see cref="AttributeValue" /> to the target CLR value recursively.</summary>
    private static object? ConvertAttributeValueToClrValueCore(
        AttributeValue attributeValue,
        Type targetType,
        CoreTypeMapping? typeMapping,
        string propertyPath,
        bool required)
    {
        if (DynamoTypeMappingSource.TryGetDictionaryValueType(
            targetType,
            out var dictionaryValueType,
            out var readOnly))
            return ConvertDictionary(
                attributeValue,
                targetType,
                dictionaryValueType,
                typeMapping?.ElementTypeMapping,
                propertyPath,
                required,
                readOnly);

        if (DynamoTypeMappingSource.TryGetSetElementType(targetType, out var setElementType))
            return ConvertSet(
                attributeValue,
                targetType,
                setElementType,
                typeMapping?.ElementTypeMapping,
                propertyPath,
                required);

        if (DynamoTypeMappingSource.TryGetListElementType(targetType, out var listElementType))
            return ConvertList(
                attributeValue,
                targetType,
                listElementType,
                typeMapping?.ElementTypeMapping,
                propertyPath,
                required);

        return ConvertPrimitive(attributeValue, targetType, typeMapping, propertyPath, required);
    }

    /// <summary>Converts a primitive wire value and applies configured EF value converter when present.</summary>
    private static object? ConvertPrimitive(
        AttributeValue attributeValue,
        Type targetType,
        CoreTypeMapping? typeMapping,
        string propertyPath,
        bool required)
    {
        var converter = typeMapping?.Converter;
        var primitiveType = converter?.ProviderClrType ?? targetType;
        var wireType = Nullable.GetUnderlyingType(primitiveType) ?? primitiveType;
        if (!TryReadPrimitiveWireValue(attributeValue, wireType, out var primitiveValue))
        {
            if (required)
                throw new InvalidOperationException(
                    $"Required property '{propertyPath}' did not contain a value for expected DynamoDB wire member '{GetExpectedWireMemberName(wireType)}'.");

            return GetDefaultValue(targetType);
        }

        var converted = converter?.ConvertFromProvider(primitiveValue) ?? primitiveValue;
        if (converted == null)
            return GetDefaultValue(targetType);

        return ConvertToTargetType(converted, targetType);
    }

    /// <summary>Converts a DynamoDB map into a strict dictionary shape.</summary>
    private static object? ConvertDictionary(
        AttributeValue attributeValue,
        Type targetType,
        Type valueType,
        CoreTypeMapping? valueMapping,
        string propertyPath,
        bool required,
        bool readOnly)
    {
        var map = attributeValue.M;
        if (map == null)
        {
            if (required)
                throw new InvalidOperationException(
                    $"Required property '{propertyPath}' did not contain a value for expected DynamoDB wire member '{nameof(AttributeValue.M)}'.");

            return GetDefaultValue(targetType);
        }

        // Always materialize mutable maps as Dictionary<string, TValue> for strict shape support.
        var dictionary =
            (IDictionary)Activator.CreateInstance(
                typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType))!;
        var valueRequired = IsNonNullableValueType(valueType);
        foreach (var pair in map)
            dictionary[pair.Key] = ConvertAttributeValueToClrValueCore(
                pair.Value,
                valueType,
                valueMapping,
                $"{propertyPath}['{pair.Key}']",
                valueRequired)!;

        if (readOnly)
            return Activator.CreateInstance(
                typeof(ReadOnlyDictionary<,>).MakeGenericType(typeof(string), valueType),
                dictionary)!;

        return dictionary;
    }

    /// <summary>Converts DynamoDB set wire members to a <see cref="HashSet{T}" />.</summary>
    private static object? ConvertSet(
        AttributeValue attributeValue,
        Type targetType,
        Type elementType,
        CoreTypeMapping? elementMapping,
        string propertyPath,
        bool required)
    {
        var providerType = elementMapping?.Converter?.ProviderClrType ?? elementType;
        var nonNullableProviderType = Nullable.GetUnderlyingType(providerType) ?? providerType;

        IEnumerable? wireValues =
            nonNullableProviderType == typeof(string) ? attributeValue.SS :
            nonNullableProviderType == typeof(byte[]) ? attributeValue.BS : attributeValue.NS;

        if (wireValues == null)
        {
            if (required)
                throw new InvalidOperationException(
                    $"Required property '{propertyPath}' did not contain a value for expected DynamoDB wire member '{GetExpectedSetWireMemberName(nonNullableProviderType)}'.");

            return GetDefaultValue(targetType);
        }

        // Strict set materialization always uses HashSet<T>.
        var set = Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(elementType))!;
        var addMethod = set.GetType().GetMethod("Add")!;

        if (nonNullableProviderType == typeof(string))
            foreach (var value in (IEnumerable<string>)wireValues)
                addMethod.Invoke(
                    set,
                    [ConvertProviderElementValue(value, elementType, elementMapping)]);
        else if (nonNullableProviderType == typeof(byte[]))
            foreach (var stream in (IEnumerable<MemoryStream>)wireValues)
                addMethod.Invoke(
                    set,
                    [ConvertProviderElementValue(stream.ToArray(), elementType, elementMapping)]);
        else
            foreach (var value in (IEnumerable<string>)wireValues)
                addMethod.Invoke(
                    set,
                    [
                        ConvertProviderElementValue(
                            ParseNumericValue(value, nonNullableProviderType),
                            elementType,
                            elementMapping),
                    ]);

        return set;
    }

    /// <summary>Converts a DynamoDB list wire member to a list or array target shape.</summary>
    private static object? ConvertList(
        AttributeValue attributeValue,
        Type targetType,
        Type elementType,
        CoreTypeMapping? elementMapping,
        string propertyPath,
        bool required)
    {
        var listValues = attributeValue.L;
        if (listValues == null)
        {
            if (required)
                throw new InvalidOperationException(
                    $"Required property '{propertyPath}' did not contain a value for expected DynamoDB wire member '{nameof(AttributeValue.L)}'.");

            return GetDefaultValue(targetType);
        }

        // Strict list materialization always starts with List<T>.
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        var elementRequired = IsNonNullableValueType(elementType);
        for (var i = 0; i < listValues.Count; i++)
            list.Add(
                ConvertAttributeValueToClrValueCore(
                    listValues[i],
                    elementType,
                    elementMapping,
                    $"{propertyPath}[{i}]",
                    elementRequired));

        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(array, 0);
            return array;
        }

        return list;
    }

    /// <summary>Reads a primitive wire value from <see cref="AttributeValue" />.</summary>
    private static bool TryReadPrimitiveWireValue(
        AttributeValue attributeValue,
        Type wireType,
        out object? value)
    {
        if (wireType == typeof(string))
        {
            value = attributeValue.S;
            return value != null;
        }

        if (wireType == typeof(bool))
        {
            value = attributeValue.BOOL;
            return value != null;
        }

        if (wireType == typeof(byte[]))
        {
            value = attributeValue.B?.ToArray();
            return value != null;
        }

        if (attributeValue.N == null)
        {
            value = null;
            return false;
        }

        value = ParseNumericValue(attributeValue.N, wireType);
        return true;
    }

    /// <summary>Parses a numeric DynamoDB wire value into a specific CLR numeric type.</summary>
    private static object ParseNumericValue(string wireValue, Type numericType)
    {
        var invariantCulture = CultureInfo.InvariantCulture;
        if (numericType == typeof(byte))
            return byte.Parse(wireValue, invariantCulture);
        if (numericType == typeof(sbyte))
            return sbyte.Parse(wireValue, invariantCulture);
        if (numericType == typeof(short))
            return short.Parse(wireValue, invariantCulture);
        if (numericType == typeof(ushort))
            return ushort.Parse(wireValue, invariantCulture);
        if (numericType == typeof(int))
            return int.Parse(wireValue, invariantCulture);
        if (numericType == typeof(uint))
            return uint.Parse(wireValue, invariantCulture);
        if (numericType == typeof(long))
            return long.Parse(wireValue, invariantCulture);
        if (numericType == typeof(ulong))
            return ulong.Parse(wireValue, invariantCulture);
        if (numericType == typeof(float))
            return float.Parse(wireValue, invariantCulture);
        if (numericType == typeof(double))
            return double.Parse(wireValue, invariantCulture);
        if (numericType == typeof(decimal))
            return decimal.Parse(wireValue, invariantCulture);

        throw new InvalidOperationException($"Unsupported numeric type '{numericType.Name}'.");
    }

    /// <summary>Applies element conversion and casts provider values to the target element CLR type.</summary>
    private static object? ConvertProviderElementValue(
        object? providerValue,
        Type elementType,
        CoreTypeMapping? elementMapping)
    {
        if (providerValue == null)
            return null;

        var converted =
            elementMapping?.Converter?.ConvertFromProvider(providerValue) ?? providerValue;
        return ConvertToTargetType(converted, elementType);
    }

    /// <summary>Converts an object to the target CLR type, including nullable and enum targets.</summary>
    private static object? ConvertToTargetType(object? value, Type targetType)
    {
        if (value == null)
            return GetDefaultValue(targetType);

        if (targetType.IsAssignableFrom(value.GetType()))
            return value;

        var nonNullableTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (nonNullableTargetType.IsEnum)
            return Enum.ToObject(nonNullableTargetType, value);

        return Convert.ChangeType(value, nonNullableTargetType, CultureInfo.InvariantCulture);
    }

    /// <summary>Returns the CLR default value for the requested type.</summary>
    private static object? GetDefaultValue(Type type)
        => type.IsValueType ? Activator.CreateInstance(type) : null;

    /// <summary>Returns the expected set wire member name for a provider element type.</summary>
    private static string GetExpectedSetWireMemberName(Type providerType)
        => providerType == typeof(string) ? nameof(AttributeValue.SS) :
            providerType == typeof(byte[]) ? nameof(AttributeValue.BS) : nameof(AttributeValue.NS);
}
