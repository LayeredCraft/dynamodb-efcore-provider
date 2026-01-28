using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage.ValueConversion;

/// <summary>
/// Converts between <see cref="string"/> and <see cref="AttributeValue"/>.
/// </summary>
public class AttributeValueToStringConverter() : ValueConverter<string, AttributeValue>(
    s => new AttributeValue { S = s },
    av => av.S ?? string.Empty);
