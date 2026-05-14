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
internal static class DynamoRuntimeValueSerializer
{
    private static readonly ConcurrentDictionary<CacheKey, Func<object?, AttributeValue>>
        AttributeValueSerializers = new();

    private static readonly ConcurrentDictionary<CacheKey, Func<object?, string>>
        LiteralSerializers = new();

    private static readonly MethodInfo ConvertProviderValueToAttributeValueMethod =
        typeof(DynamoWireValueConversion).GetMethod(
            nameof(DynamoWireValueConversion.ConvertProviderValueToAttributeValue))!;

    private static readonly MethodInfo GenerateBoxedConstantMethod =
        typeof(DynamoWireValueConversion).GetMethod(
            nameof(DynamoWireValueConversion.GenerateBoxedConstant))!;

    public static AttributeValue CreateAttributeValue(
        DynamoTypeMapping mapping,
        object? value,
        Type? sourceType = null)
    {
        if (value is null)
            return new AttributeValue { NULL = true };

        sourceType ??= value.GetType();
        return AttributeValueSerializers.GetOrAdd(
            new CacheKey(mapping, sourceType),
            static key => CompileAttributeValueSerializer(key.Mapping, key.SourceType))(value);
    }

    public static string GenerateLiteral(
        DynamoTypeMapping mapping,
        object? value,
        Type? sourceType = null)
    {
        if (value is null)
            return "NULL";

        sourceType ??= value.GetType();
        return LiteralSerializers.GetOrAdd(
            new CacheKey(mapping, sourceType),
            static key => CompileLiteralSerializer(key.Mapping, key.SourceType))(value);
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
        return IsNumericType(nonNullableMappingType) && IsNumericType(nonNullableSourceType);
    }

    private static bool IsNumericType(Type type)
        => type.IsEnum
            || type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);

    private readonly record struct CacheKey(DynamoTypeMapping Mapping, Type SourceType);
}
