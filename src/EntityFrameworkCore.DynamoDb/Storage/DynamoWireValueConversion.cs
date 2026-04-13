using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.Storage;

internal static class DynamoWireValueConversion
{
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
        => new() { B = new MemoryStream(value, false) };

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
