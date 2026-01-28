using System.Globalization;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage.ValueConversion;

/// <summary>
/// Converts between <see cref="float"/> and <see cref="AttributeValue"/>.
/// DynamoDB stores numbers as strings in the N property.
/// </summary>
public class AttributeValueToFloatConverter() : ValueConverter<float, AttributeValue>(
    f => new AttributeValue { N = f.ToString(CultureInfo.InvariantCulture) },
    av => float.Parse(av.N, CultureInfo.InvariantCulture));
