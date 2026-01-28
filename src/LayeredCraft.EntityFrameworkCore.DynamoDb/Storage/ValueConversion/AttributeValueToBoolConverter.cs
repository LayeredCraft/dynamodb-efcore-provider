using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage.ValueConversion;

/// <summary>
/// Converts between <see cref="bool"/> and <see cref="AttributeValue"/>.
/// </summary>
public class AttributeValueToBoolConverter() : ValueConverter<bool, AttributeValue>(
    b => new AttributeValue { BOOL = b },
    av => av.BOOL ?? false);
