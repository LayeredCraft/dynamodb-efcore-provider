using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using LayeredCraft.DynamoMapper.Runtime;

// ReSharper disable ClassNeverInstantiated.Global

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

public static class MapperExtensions
{
    public static CustomerItem ToCustomerItem(this Dictionary<string, AttributeValue> item)
        => SaveChangesCustomerItemMapper.FromItem(item);

    public static OrderItem ToOrderItem(this Dictionary<string, AttributeValue> item)
        => SaveChangesOrderItemMapper.FromItem(item);

    public static ConverterCoverageItem ToConverterCoverageItem(
        this Dictionary<string, AttributeValue> item)
        => SaveChangesConverterCoverageItemMapper.FromItem(item);
}

[DynamoMapper(
    Convention = DynamoNamingConvention.CamelCase,
    OmitNullValues = false,
    DateTimeFormat = "yyyy-MM-dd HH:mm:sszzz")]
[DynamoIgnore(nameof(CustomerItem.ReferenceIds), Ignore = IgnoreMapping.ToModel)]
internal partial class SaveChangesCustomerItemMapper : IDynamoMapper<CustomerItem>
{
    public static partial Dictionary<string, AttributeValue> ToItem(CustomerItem source);

    public static partial CustomerItem FromItem(Dictionary<string, AttributeValue> item);
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal partial class SaveChangesOrderItemMapper : IDynamoMapper<OrderItem>
{
    public static partial Dictionary<string, AttributeValue> ToItem(OrderItem source);

    public static partial OrderItem FromItem(Dictionary<string, AttributeValue> item);
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal partial class SaveChangesProductItemMapper : IDynamoMapper<ProductItem>
{
    public static partial Dictionary<string, AttributeValue> ToItem(ProductItem source);

    public static partial ProductItem FromItem(Dictionary<string, AttributeValue> item);
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal partial class SaveChangesSessionItemMapper : IDynamoMapper<SessionItem>
{
    public static partial Dictionary<string, AttributeValue> ToItem(SessionItem source);

    public static partial SessionItem FromItem(Dictionary<string, AttributeValue> item);
}

[DynamoMapper(
    Convention = DynamoNamingConvention.CamelCase,
    OmitNullValues = false,
    DateTimeFormat = "yyyy-MM-dd HH:mm:sszzz")]
internal partial class SaveChangesConverterCoverageItemMapper : IDynamoMapper<ConverterCoverageItem>
{
    public static partial Dictionary<string, AttributeValue> ToItem(ConverterCoverageItem source);

    public static partial ConverterCoverageItem FromItem(Dictionary<string, AttributeValue> item);
}
