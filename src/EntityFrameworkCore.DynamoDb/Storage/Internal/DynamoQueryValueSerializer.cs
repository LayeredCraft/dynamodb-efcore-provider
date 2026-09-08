using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.Storage.Internal;

/// <summary>
///     Serializes EF's object-shaped runtime value boundary through mapping-owned DynamoDB codecs.
/// </summary>
/// <remarks>
///     <para>
///         EF hands query constants and parameters to type mappings as boxed <see cref="object" />
///         values. This adapter is the only intended query serialization boxing boundary: it
///         unboxes once to the source CLR type, then dispatches to hand-written codecs. No
///         expression trees are built here, so precompiled-query execution stays NativeAOT-safe
///         (no <c>Expression.Compile</c> and no runtime <c>MakeGenericMethod</c>).
///     </para>
///     <para>
///         Numeric promotions (for example a <see cref="short" /> property compared to an
///         <see cref="int" /> parameter) serialize using the runtime source type, preserving the
///         numeric DynamoDB value without unboxing as the property's CLR type.
///     </para>
/// </remarks>
internal static class DynamoQueryValueSerializer
{
    /// <summary>Serializes a boxed runtime value to a DynamoDB attribute value.</summary>
    /// <param name="mapping">DynamoDB type mapping that owns conversion metadata.</param>
    /// <param name="value">Boxed runtime value to serialize.</param>
    /// <param name="sourceType">Known runtime/source CLR type, or <see langword="null" /> to infer it.</param>
    /// <returns>DynamoDB attribute value for the runtime value.</returns>
    internal static AttributeValue CreateAttributeValue(
        DynamoTypeMapping mapping,
        object? value,
        Type? sourceType = null)
    {
        if (value is null && mapping.Converter?.ConvertsNulls != true)
            return new AttributeValue { NULL = true };

        sourceType = NormalizeSourceType(value, sourceType, mapping.ClrType);

        if (CanUseMappingExpression(mapping, sourceType))
            return RequireReaderWriter(mapping, false).WriteBoxed(value);

        if (mapping.Converter is null && IsNumericCompatible(mapping.ClrType, sourceType))
            return CreateNumericAttributeValue(value!, sourceType);

        if (mapping.Converter is not null)
            return RequireReaderWriter(mapping, false)
                .WriteBoxed(ConvertToMappingClrType(value!, mapping.ClrType));

        throw CreateCoercionError(mapping.ClrType, sourceType);
    }

    /// <summary>Generates a PartiQL literal for a boxed runtime value.</summary>
    /// <param name="mapping">DynamoDB type mapping that owns conversion metadata.</param>
    /// <param name="value">Boxed runtime value to render.</param>
    /// <param name="sourceType">Known runtime/source CLR type, or <see langword="null" /> to infer it.</param>
    /// <returns>PartiQL literal for the runtime value.</returns>
    internal static string GenerateLiteral(
        DynamoTypeMapping mapping,
        object? value,
        Type? sourceType = null)
    {
        if (value is null && mapping.Converter?.ConvertsNulls != true)
            return "NULL";

        sourceType = NormalizeSourceType(value, sourceType, mapping.ClrType);

        if (CanUseMappingExpression(mapping, sourceType))
            return RequireReaderWriter(mapping, true).ToPartiQlLiteralBoxed(value);

        if (mapping.Converter is null && IsNumericCompatible(mapping.ClrType, sourceType))
            // Inline constants follow the same source-type formatting rule as AttributeValues.
            return DynamoWireValueConversion.GenerateBoxedConstant(value);

        if (mapping.Converter is not null)
            return RequireReaderWriter(mapping, true)
                .ToPartiQlLiteralBoxed(ConvertToMappingClrType(value!, mapping.ClrType));

        throw CreateCoercionError(mapping.ClrType, sourceType);
    }

    private static Type NormalizeSourceType(object? value, Type? sourceType, Type clrType)
    {
        var resolvedType = value is null ? clrType : sourceType ?? value.GetType();

        if (value is not null
                && Nullable.GetUnderlyingType(resolvedType) is { } underlyingSourceType)
            // A boxed nullable value is the boxed underlying, so dispatch on the runtime type.
            return underlyingSourceType;

        return resolvedType;
    }

    private static DynamoValueReaderWriter RequireReaderWriter(
        DynamoTypeMapping mapping,
        bool valueExpressionType)
        => mapping.ReaderWriter
            ?? throw new NotSupportedException(
                $"CLR type '{mapping.ClrType.Name}' is not supported for DynamoDB "
                + (valueExpressionType
                    ? "PartiQL constant generation."
                    : "AttributeValue serialization."));

    private static AttributeValue CreateNumericAttributeValue(object value, Type sourceType)
        => sourceType.IsEnum
            // Boxed enum values of any enum type share the object instantiation, which formats
            // via FormatEnum inside the wire conversion.
            ? DynamoWireValueConversion.ConvertProviderValueToAttributeValue<object>(value)
            : sourceType switch
            {
                _ when sourceType == typeof(byte) => DynamoWireValueConversion
                    .ConvertProviderValueToAttributeValue<byte>((byte)value),
                _ when sourceType == typeof(sbyte) => DynamoWireValueConversion
                    .ConvertProviderValueToAttributeValue<sbyte>((sbyte)value),
                _ when sourceType == typeof(short) => DynamoWireValueConversion
                    .ConvertProviderValueToAttributeValue<short>((short)value),
                _ when sourceType == typeof(ushort) => DynamoWireValueConversion
                    .ConvertProviderValueToAttributeValue<ushort>((ushort)value),
                _ when sourceType == typeof(int) => DynamoWireValueConversion
                    .ConvertProviderValueToAttributeValue<int>((int)value),
                _ when sourceType == typeof(uint) => DynamoWireValueConversion
                    .ConvertProviderValueToAttributeValue<uint>((uint)value),
                _ when sourceType == typeof(long) => DynamoWireValueConversion
                    .ConvertProviderValueToAttributeValue<long>((long)value),
                _ when sourceType == typeof(ulong) => DynamoWireValueConversion
                    .ConvertProviderValueToAttributeValue<ulong>((ulong)value),
                _ when sourceType == typeof(float) => DynamoWireValueConversion
                    .ConvertProviderValueToAttributeValue<float>((float)value),
                _ when sourceType == typeof(double) => DynamoWireValueConversion
                    .ConvertProviderValueToAttributeValue<double>((double)value),
                _ when sourceType == typeof(decimal) => DynamoWireValueConversion
                    .ConvertProviderValueToAttributeValue<decimal>((decimal)value),
                // IsNumericCompatible guarantees a numeric source type; the fallthrough is
                // defensive.
                _ => throw CreateCoercionError(typeof(object), sourceType)
            };

    /// <summary>
    ///     Replicates the expression pipeline's <c>Expression.Convert</c> fallback for converter
    ///     mappings whose runtime value type does not match the mapped model type (for example an
    ///     enum constant boxed as its underlying integer, or a boxed value passed as
    ///     <see cref="object" />), without building expression trees.
    /// </summary>
    private static object ConvertToMappingClrType(object value, Type targetType)
    {
        var runtimeType = value.GetType();

        if (targetType == runtimeType)
            return value;

        var effectiveTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (effectiveTarget == runtimeType || targetType.IsAssignableFrom(runtimeType))
            return value;

        if (effectiveTarget.IsEnum)
            if (runtimeType.IsEnum || DynamoWireValueConversion.IsNumericType(runtimeType))
                // Convert through the enum's underlying type so cross-enum sources and nullable
                // enum targets both land on the right enum instance. Conversion to a narrower
                // underlying type that cannot hold the source value throws OverflowException,
                // which is intentional: out-of-range coercions must not silently truncate.
                return Enum.ToObject(
                    effectiveTarget,
                    Convert.ChangeType(
                        value,
                        Enum.GetUnderlyingType(effectiveTarget),
                        CultureInfo.InvariantCulture));

        if (DynamoWireValueConversion.IsNumericType(effectiveTarget)
            && DynamoWireValueConversion.IsNumericType(runtimeType))
            return Convert.ChangeType(value, effectiveTarget, CultureInfo.InvariantCulture);

        throw CreateCoercionError(effectiveTarget, runtimeType);
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

    private static InvalidOperationException CreateCoercionError(Type targetType, Type sourceType)
        // Match the framework message shape callers previously observed from expression-tree
        // construction so failure behavior is stable across the AOT rewrite.
        => new($"No coercion operator is defined between types '{sourceType}' and '{targetType}'.");
}
