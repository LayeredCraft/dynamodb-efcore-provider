using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

public static class AttributeValueExtensions
{
    public static AttributeValue ToAttributeValue(this string value) => new() { S = value };

    public static AttributeValue ToAttributeValue(this int value) => new() { N = value.ToString() };

    public static AttributeValue ToAttributeValue(this byte[] value)
        => new() { B = new MemoryStream(value) };
}
