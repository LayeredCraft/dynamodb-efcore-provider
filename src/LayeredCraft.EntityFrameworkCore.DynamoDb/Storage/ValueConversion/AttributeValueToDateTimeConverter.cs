using System.Globalization;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage.ValueConversion;

/// <summary>
/// Converts between <see cref="DateTime"/> and <see cref="AttributeValue"/>.
/// Supports both ISO8601 string format and Unix timestamp (numeric) format.
/// </summary>
public class AttributeValueToDateTimeConverter() : ValueConverter<DateTime, AttributeValue>(
    dt => new AttributeValue { S = dt.ToString("O") },
    av => ParseDateTime(av))
{
    // ISO8601 round-trip format

    private static DateTime ParseDateTime(AttributeValue av)
    {
        // Try ISO8601 string first
        if (!string.IsNullOrEmpty(av.S))
            return DateTime.Parse(av.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        // Fall back to Unix timestamp
        if (!string.IsNullOrEmpty(av.N))
            return DateTimeOffset
                .FromUnixTimeSeconds(long.Parse(av.N, CultureInfo.InvariantCulture))
                .DateTime;

        return default;
    }
}
