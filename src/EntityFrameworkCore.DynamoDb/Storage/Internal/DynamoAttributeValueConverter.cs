using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Storage.Internal;

/// <summary>Converts CLR values to DynamoDB <see cref="AttributeValue" /> instances.</summary>
internal static class DynamoAttributeValueConverter
{
    /// <summary>
    ///     Converts a CLR value to a DynamoDB <see cref="AttributeValue" />, applying any
    ///     value converter from the type mapping when present.
    /// </summary>
    /// <param name="value">The CLR value to convert. May be <c>null</c>.</param>
    /// <param name="typeMapping">
    ///     Optional type mapping whose converter is applied before the primitive conversion.
    /// </param>
    /// <returns>
    ///     An <see cref="AttributeValue" /> representing the value, or <c>{ NULL = true }</c>
    ///     when <paramref name="value" /> is <c>null</c>.
    /// </returns>
    /// <exception cref="NotSupportedException">
    ///     Thrown when the CLR type has no registered DynamoDB primitive mapping.
    /// </exception>
    internal static AttributeValue Convert(object? value, CoreTypeMapping? typeMapping)
    {
        // Apply value converter if present
        if (typeMapping?.Converter != null && value != null)
            value = typeMapping.Converter.ConvertToProvider(value);

        if (value == null)
            return new AttributeValue { NULL = true };

        return value switch
        {
            string s => new AttributeValue { S = s },
            bool b => new AttributeValue { BOOL = b },
            int i => new AttributeValue { N = i.ToString() },
            long l => new AttributeValue { N = l.ToString() },
            short sh => new AttributeValue { N = sh.ToString() },
            byte by => new AttributeValue { N = by.ToString() },
            double d => new AttributeValue { N = d.ToString("R") },
            float f => new AttributeValue { N = f.ToString("R") },
            decimal dec => new AttributeValue { N = dec.ToString() },
            Guid g => new AttributeValue { S = g.ToString() },
            DateTime dt => new AttributeValue { S = dt.ToString("O") },
            DateTimeOffset dto => new AttributeValue { S = dto.ToString("O") },
            _ => throw new NotSupportedException(
                $"Type {value.GetType()} is not supported for conversion to AttributeValue"),
        };
    }
}
