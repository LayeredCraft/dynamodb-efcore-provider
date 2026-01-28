using System.Globalization;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage.ValueConversion;

/// <summary>
/// Converts between <see cref="double"/> and <see cref="AttributeValue"/>.
/// DynamoDB stores numbers as strings in the N property.
/// </summary>
public class AttributeValueToDoubleConverter() : ValueConverter<double, AttributeValue>(
    d => new AttributeValue { N = d.ToString(CultureInfo.InvariantCulture) },
    av => double.Parse(av.N, CultureInfo.InvariantCulture));
