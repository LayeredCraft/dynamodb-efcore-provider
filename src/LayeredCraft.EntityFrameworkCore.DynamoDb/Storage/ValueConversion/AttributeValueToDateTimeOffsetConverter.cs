using System.Globalization;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage.ValueConversion;

/// <summary>
/// Converts between <see cref="DateTimeOffset"/> and <see cref="AttributeValue"/>.
/// Supports both ISO8601 string format and Unix timestamp (numeric) format.
/// </summary>
public class AttributeValueToDateTimeOffsetConverter()
    : ValueConverter<DateTimeOffset, AttributeValue>(
        dto => new AttributeValue { S = dto.ToString("O") },
        av => ParseDateTimeOffset(av))
{
    // ISO8601 round-trip format

    private static DateTimeOffset ParseDateTimeOffset(AttributeValue av)
    {
        // Try ISO8601 string first
        if (!string.IsNullOrEmpty(av.S))
            return DateTimeOffset.Parse(
                av.S,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);

        // Fall back to Unix timestamp
        if (!string.IsNullOrEmpty(av.N))
            return DateTimeOffset.FromUnixTimeSeconds(
                long.Parse(av.N, CultureInfo.InvariantCulture));

        return default;
    }
}
