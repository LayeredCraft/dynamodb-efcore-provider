using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedTable;

[DynamoMapper(
    Convention = DynamoNamingConvention.Exact,
    OmitNullValues = false,
    IncludeBaseClassProperties = true)]
internal static partial class UserEntityMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(UserEntity source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(List<UserEntity> sources)
        => sources.Select(ToItem).ToList();

    private static void AfterToItem(UserEntity source, Dictionary<string, AttributeValue> item)
    {
        item["$type"] = new AttributeValue { S = "UserEntity" };
        item["$kind"] = new AttributeValue { S = "UserEntity" };
    }
}

[DynamoMapper(
    Convention = DynamoNamingConvention.Exact,
    OmitNullValues = false,
    IncludeBaseClassProperties = true)]
internal static partial class OrderEntityMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(OrderEntity source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(List<OrderEntity> sources)
        => sources.Select(ToItem).ToList();

    private static void AfterToItem(OrderEntity source, Dictionary<string, AttributeValue> item)
    {
        item["$type"] = new AttributeValue { S = "OrderEntity" };
        item["$kind"] = new AttributeValue { S = "OrderEntity" };
    }
}

[DynamoMapper(
    Convention = DynamoNamingConvention.Exact,
    OmitNullValues = false,
    IncludeBaseClassProperties = true)]
internal static partial class EmployeeEntityMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(EmployeeEntity source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(List<EmployeeEntity> sources)
        => sources.Select(ToItem).ToList();

    private static void AfterToItem(EmployeeEntity source, Dictionary<string, AttributeValue> item)
    {
        item["$type"] = new AttributeValue { S = "EmployeeEntity" };
        item["$kind"] = new AttributeValue { S = "EmployeeEntity" };
    }
}

[DynamoMapper(
    Convention = DynamoNamingConvention.Exact,
    OmitNullValues = false,
    IncludeBaseClassProperties = true)]
[DynamoField("Level", AttributeName = "ManagerLevel")]
internal static partial class ManagerEntityMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(ManagerEntity source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(List<ManagerEntity> sources)
        => sources.Select(ToItem).ToList();

    private static void AfterToItem(ManagerEntity source, Dictionary<string, AttributeValue> item)
    {
        item["$type"] = new AttributeValue { S = "ManagerEntity" };
        item["$kind"] = new AttributeValue { S = "ManagerEntity" };
    }
}
