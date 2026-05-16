using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

public interface IDynamoMapper<T>
{
    static abstract Dictionary<string, AttributeValue> ToItem(T source);
    static abstract T FromItem(Dictionary<string, AttributeValue> item);
}
