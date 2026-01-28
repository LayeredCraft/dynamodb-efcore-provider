using System.Globalization;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage.ValueConversion;

/// <summary>
/// Converts between <see cref="long"/> and <see cref="AttributeValue"/>.
/// DynamoDB stores numbers as strings in the N property.
/// </summary>
public class AttributeValueToLongConverter() : ValueConverter<long, AttributeValue>(
    l => new AttributeValue { N = l.ToString(CultureInfo.InvariantCulture) },
    av => long.Parse(av.N, CultureInfo.InvariantCulture));
