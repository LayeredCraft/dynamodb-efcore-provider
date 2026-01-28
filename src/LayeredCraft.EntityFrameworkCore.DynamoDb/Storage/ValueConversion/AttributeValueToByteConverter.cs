using System.Globalization;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage.ValueConversion;

/// <summary>
/// Converts between <see cref="byte"/> and <see cref="AttributeValue"/>.
/// DynamoDB stores numbers as strings in the N property.
/// </summary>
public class AttributeValueToByteConverter() : ValueConverter<byte, AttributeValue>(
    b => new AttributeValue { N = b.ToString(CultureInfo.InvariantCulture) },
    av => byte.Parse(av.N, CultureInfo.InvariantCulture));
