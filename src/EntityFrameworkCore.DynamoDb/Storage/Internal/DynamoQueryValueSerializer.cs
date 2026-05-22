using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.Storage.Internal;

/// <summary>
///     Compiles typed serializers for EF's object-shaped runtime value boundary.
/// </summary>
/// <remarks>
///     EF hands query constants and parameters to type mappings as boxed <see cref="object" />
///     values. This adapter is the only intended query serialization boxing boundary: it unboxes
///     once to the source CLR type, then resumes the mapping-owned expression pipeline so value
///     converters use typed expressions instead of <c>ConvertToProvider(object)</c>.
/// </remarks>
internal static class DynamoQueryValueSerializer
{
    private static readonly MethodInfo ConvertProviderValueToAttributeValueMethod =
        typeof(DynamoWireValueConversion).GetMethod(
            nameof(DynamoWireValueConversion.ConvertProviderValueToAttributeValue))!;

    private static readonly MethodInfo GenerateBoxedConstantMethod =
        typeof(DynamoWireValueConversion).GetMethod(
            nameof(DynamoWireValueConversion.GenerateBoxedConstant))!;

    /// <summary>Serializes a boxed runtime value through a mapping-owned typed delegate cache.</summary>
    /// <param name="mapping">DynamoDB type mapping that owns conversion metadata.</param>
    /// <param name="serializers">Per-mapping serializer cache keyed by runtime source type.</param>
    /// <param name="value">Boxed runtime value to serialize.</param>
    /// <param name="sourceType">Known runtime/source CLR type, or <see langword="null" /> to infer it.</param>
    /// <returns>DynamoDB attribute value for the runtime value.</returns>
    internal static AttributeValue CreateAttributeValue(
        DynamoTypeMapping mapping,
        ConcurrentDictionary<Type, Func<object?, AttributeValue>> serializers,
        object? value,
        Type? sourceType = null)
    {
        if (value is null && mapping.Converter?.ConvertsNulls != true)
            return new AttributeValue { NULL = true };

        sourceType = value is null ? mapping.ClrType : sourceType ?? value.GetType();
        return serializers.GetOrAdd(
            sourceType,
            static (key, mapping) => CompileAttributeValueSerializer(mapping, key),
            mapping)(value);
    }

    /// <summary>Generates a PartiQL literal through a mapping-owned typed delegate cache.</summary>
    /// <param name="mapping">DynamoDB type mapping that owns conversion metadata.</param>
    /// <param name="serializers">Per-mapping literal serializer cache keyed by runtime source type.</param>
    /// <param name="value">Boxed runtime value to render.</param>
    /// <param name="sourceType">Known runtime/source CLR type, or <see langword="null" /> to infer it.</param>
    /// <returns>PartiQL literal for the runtime value.</returns>
    internal static string GenerateLiteral(
        DynamoTypeMapping mapping,
        ConcurrentDictionary<Type, Func<object?, string>> serializers,
        object? value,
        Type? sourceType = null)
    {
        if (value is null && mapping.Converter?.ConvertsNulls != true)
            return "NULL";

        sourceType = value is null ? mapping.ClrType : sourceType ?? value.GetType();
        return serializers.GetOrAdd(
            sourceType,
            static (key, mapping) => CompileLiteralSerializer(mapping, key),
            mapping)(value);
    }

    private static Func<object?, AttributeValue> CompileAttributeValueSerializer(
        DynamoTypeMapping mapping,
        Type sourceType)
    {
        var valueParameter = Expression.Parameter(typeof(object), "value");
        var typedValue = Expression.Convert(valueParameter, sourceType);
        var body = CreateAttributeValueBody(mapping, typedValue, sourceType);
        return Expression.Lambda<Func<object?, AttributeValue>>(body, valueParameter).Compile();
    }

    private static Func<object?, string> CompileLiteralSerializer(
        DynamoTypeMapping mapping,
        Type sourceType)
    {
        var valueParameter = Expression.Parameter(typeof(object), "value");
        var typedValue = Expression.Convert(valueParameter, sourceType);
        var body = CreateLiteralBody(mapping, typedValue, sourceType);
        return Expression.Lambda<Func<object?, string>>(body, valueParameter).Compile();
    }

    private static Expression CreateAttributeValueBody(
        DynamoTypeMapping mapping,
        Expression typedValue,
        Type sourceType)
    {
        if (CanUseMappingExpression(mapping, sourceType))
            return mapping.CreateAttributeValueExpression(typedValue);

        // Numeric promotions (for example short property compared to int parameter) should stay
        // numeric DynamoDB values without unboxing the runtime value as the property's CLR type.
        if (mapping.Converter is null && IsNumericCompatible(mapping.ClrType, sourceType))
            return Expression.Call(
                ConvertProviderValueToAttributeValueMethod.MakeGenericMethod(sourceType),
                typedValue);

        return mapping.CreateAttributeValueExpression(
            Expression.Convert(typedValue, mapping.ClrType));
    }

    private static Expression CreateLiteralBody(
        DynamoTypeMapping mapping,
        Expression typedValue,
        Type sourceType)
    {
        if (CanUseMappingExpression(mapping, sourceType))
            return mapping.CreatePartiQlLiteralExpression(typedValue);

        // Inline constants follow the same numeric-promotion rule as AttributeValue parameters.
        if (mapping.Converter is null && IsNumericCompatible(mapping.ClrType, sourceType))
            return Expression.Call(
                GenerateBoxedConstantMethod,
                Expression.Convert(typedValue, typeof(object)));

        return mapping.CreatePartiQlLiteralExpression(
            Expression.Convert(typedValue, mapping.ClrType));
    }

    private static bool CanUseMappingExpression(DynamoTypeMapping mapping, Type sourceType)
    {
        var targetType = mapping.ClrType;
        return targetType == sourceType
            || targetType.IsAssignableFrom(sourceType)
            || (Nullable.GetUnderlyingType(targetType) is { } targetUnderlying
                && targetUnderlying == sourceType);
    }

    private static bool IsNumericCompatible(Type mappingType, Type sourceType)
    {
        var nonNullableMappingType = Nullable.GetUnderlyingType(mappingType) ?? mappingType;
        var nonNullableSourceType = Nullable.GetUnderlyingType(sourceType) ?? sourceType;
        return DynamoWireValueConversion.IsNumericType(nonNullableMappingType)
            && DynamoWireValueConversion.IsNumericType(nonNullableSourceType);
    }
}
