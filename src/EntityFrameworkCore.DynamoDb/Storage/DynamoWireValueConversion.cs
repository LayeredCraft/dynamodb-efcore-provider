using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.Storage;

internal static class DynamoWireValueConversion
{
    /// <summary>Returns whether the CLR type is represented as a numeric DynamoDB value.</summary>
    /// <param name="type">CLR type to inspect.</param>
    /// <returns><see langword="true" /> when values of <paramref name="type" /> use DynamoDB number wire values.</returns>
    public static bool IsNumericType(Type type)
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

    /// <summary>Returns whether the CLR type is an integral numeric type.</summary>
    /// <param name="type">CLR type to inspect.</param>
    /// <returns><see langword="true" /> when <paramref name="type" /> is an integral numeric type.</returns>
    public static bool IsIntegralType(Type type)
        => type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong);

    /// <summary>Returns whether an integral type can represent an enum underlying type.</summary>
    /// <param name="valueType">Candidate value type.</param>
    /// <param name="underlyingType">Enum underlying type.</param>
    /// <returns><see langword="true" /> when both types are integral numeric types.</returns>
    public static bool CanRepresentEnumUnderlyingType(Type valueType, Type underlyingType)
        => IsIntegralType(valueType) && IsIntegralType(underlyingType);

    /// <summary>Formats an enum as its numeric DynamoDB wire value.</summary>
    /// <param name="value">Enum value to format.</param>
    /// <returns>Invariant-culture numeric representation of <paramref name="value" />.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the enum has an unsupported underlying type.</exception>
    public static string FormatEnum(object value)
        => Type.GetTypeCode(Enum.GetUnderlyingType(value.GetType())) switch
        {
            TypeCode.Byte => Convert
                .ToByte(value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture),
            TypeCode.SByte => Convert
                .ToSByte(value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture),
            TypeCode.Int16 => Convert
                .ToInt16(value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture),
            TypeCode.UInt16 => Convert
                .ToUInt16(value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture),
            TypeCode.Int32 => Convert
                .ToInt32(value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture),
            TypeCode.UInt32 => Convert
                .ToUInt32(value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture),
            TypeCode.Int64 => Convert
                .ToInt64(value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture),
            TypeCode.UInt64 => Convert
                .ToUInt64(value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException(
                $"Enum type '{value.GetType().Name}' has an unsupported underlying type."),
        };

    /// <summary>Formats numeric values for DynamoDB wire output using invariant culture.</summary>
    /// <remarks>
    ///     Uses round-trip (<c>R</c>) formatting for <see cref="float" /> and <see cref="double" />
    ///     to preserve precision across write/read cycles.
    /// </remarks>
    public static string FormatNumber<T>(T value) where T : struct, IFormattable
        => value.ToString(
            typeof(T) == typeof(float) || typeof(T) == typeof(double) ? "R" : null,
            CultureInfo.InvariantCulture);

    /// <summary>Creates a binary DynamoDB attribute value from a byte array.</summary>
    /// <remarks>
    ///     Wraps the byte array in a non-writable <see cref="MemoryStream" /> so the SDK can stream
    ///     bytes without mutating the original array.
    /// </remarks>
    public static AttributeValue CreateBinaryAttributeValue(byte[] value)
        => new() { B = CreateBinaryStream(value) };

    /// <summary>Creates a stream wrapper for a DynamoDB binary attribute.</summary>
    /// <remarks>
    ///     The stream is handed to the AWS SDK through <see cref="AttributeValue.B" /> or
    ///     <see cref="AttributeValue.BS" /> and must remain open until request serialization. The
    ///     <see cref="MemoryStream" /> wraps caller-owned memory and does not hold unmanaged resources.
    /// </remarks>
    internal static MemoryStream CreateBinaryStream(byte[] value) => new(value, writable: false);

    /// <summary>Converts a provider CLR value to a DynamoDB <see cref="AttributeValue" />.</summary>
    /// <remarks>
    ///     Null values are represented as <c>{ NULL = true }</c>. Supported provider CLR shapes are
    ///     string, bool, numeric primitives, and <c>byte[]</c>.
    /// </remarks>
    /// <exception cref="NotSupportedException">
    ///     Thrown when <typeparamref name="T" /> is not a supported
    ///     DynamoDB wire type.
    /// </exception>
    public static AttributeValue ConvertProviderValueToAttributeValue<T>(T value)
    {
        if (value is null)
            return new AttributeValue { NULL = true };

        if (value.GetType().IsEnum)
            return new AttributeValue { N = FormatEnum(value) };

        return value switch
        {
            string s => new AttributeValue { S = s },
            bool b => new AttributeValue { BOOL = b },
            byte by => new AttributeValue { N = FormatNumber(by) },
            sbyte sb => new AttributeValue { N = FormatNumber(sb) },
            short sh => new AttributeValue { N = FormatNumber(sh) },
            ushort ush => new AttributeValue { N = FormatNumber(ush) },
            int i => new AttributeValue { N = FormatNumber(i) },
            uint ui => new AttributeValue { N = FormatNumber(ui) },
            long l => new AttributeValue { N = FormatNumber(l) },
            ulong ul => new AttributeValue { N = FormatNumber(ul) },
            float f => new AttributeValue { N = FormatNumber(f) },
            double d => new AttributeValue { N = FormatNumber(d) },
            decimal dec => new AttributeValue { N = FormatNumber(dec) },
            byte[] bytes => CreateBinaryAttributeValue(bytes),
            _ => throw new NotSupportedException(
                $"Type {value.GetType()} is not supported for DynamoDB wire conversion"),
        };
    }

    /// <summary>Generates an inline PartiQL constant for a boxed CLR value.</summary>
    /// <remarks>
    ///     String values are escaped for single-quoted PartiQL literals. Binary values are rejected
    ///     because inline binary literals are not supported by this provider path; use parameters instead.
    /// </remarks>
    public static string GenerateBoxedConstant(object? value)
    {
        if (value == null)
            return "NULL";

        if (value.GetType().IsEnum)
            return FormatEnum(value);

        return value switch
        {
            string s => $"'{s.Replace("'", "''")}'",
            bool b => b ? "TRUE" : "FALSE",
            byte by => FormatNumber(by),
            sbyte sb => FormatNumber(sb),
            short sh => FormatNumber(sh),
            ushort ush => FormatNumber(ush),
            int i => FormatNumber(i),
            uint ui => FormatNumber(ui),
            long l => FormatNumber(l),
            ulong ul => FormatNumber(ul),
            float f => FormatNumber(f),
            double d => FormatNumber(d),
            decimal dec => FormatNumber(dec),
            byte[] => throw new NotSupportedException(
                "Inline PartiQL constants for binary values are not supported. Use parameters instead."),
            _ => throw new NotSupportedException(
                $"Type {value.GetType()} is not supported for SQL constant generation"),
        };
    }
}
