using System.Globalization;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage.ValueConversion;

/// <summary>
/// Converts between <see cref="int"/> and <see cref="AttributeValue"/>.
/// DynamoDB stores numbers as strings in the N property.
/// </summary>
public class AttributeValueToIntConverter() : ValueConverter<int, AttributeValue>(
    i => new AttributeValue { N = i.ToString(CultureInfo.InvariantCulture) },
    av => int.Parse(av.N, CultureInfo.InvariantCulture));
