using System.Globalization;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage.ValueConversion;

/// <summary>
/// Converts between <see cref="short"/> and <see cref="AttributeValue"/>.
/// DynamoDB stores numbers as strings in the N property.
/// </summary>
public class AttributeValueToShortConverter() : ValueConverter<short, AttributeValue>(
    s => new AttributeValue { N = s.ToString(CultureInfo.InvariantCulture) },
    av => short.Parse(av.N, CultureInfo.InvariantCulture));
