using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage.ValueConversion;

/// <summary>
/// Converts between <see cref="Guid"/> and <see cref="AttributeValue"/>.
/// Guids are stored as strings in DynamoDB.
/// </summary>
public class AttributeValueToGuidConverter() : ValueConverter<Guid, AttributeValue>(
    g => new AttributeValue { S = g.ToString() },
    av => Guid.Parse(av.S));
