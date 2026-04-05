using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.Storage;

internal static class DynamoWireValueConversion
{
    public static string FormatNumber<T>(T value) where T : struct, IFormattable
        => value.ToString(
            typeof(T) == typeof(float) || typeof(T) == typeof(double) ? "R" : null,
            CultureInfo.InvariantCulture);

    public static AttributeValue CreateBinaryAttributeValue(byte[] value)
        => new() { B = new MemoryStream(value, false) };

    public static AttributeValue ConvertProviderValueToAttributeValue<T>(T value)
    {
        if (value == null)
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

    public static AttributeValue ConvertBoxedProviderValueToAttributeValue(object? value)
    {
        if (value == null)
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
