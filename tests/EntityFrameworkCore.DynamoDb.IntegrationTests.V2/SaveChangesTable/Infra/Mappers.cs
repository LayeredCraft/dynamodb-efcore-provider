using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SaveChangesTable;

[DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullValues = false)]
[DynamoField(nameof(CustomerItem.CreatedAt), Format = "yyyy-MM-dd HH:mm:sszzz")]
[DynamoField(
    nameof(CustomerItem.Contacts) + "." + nameof(CustomerContact.VerifiedAt),
    Format = "yyyy-MM-dd HH:mm:sszzz")]
[DynamoIgnore(nameof(CustomerItem.ReferenceIds), Ignore = IgnoreMapping.ToModel)]
internal static partial class SaveChangesCustomerItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(CustomerItem source);

    internal static partial CustomerItem FromItem(Dictionary<string, AttributeValue> item);

    internal static CustomerItem ToCustomerItem(this Dictionary<string, AttributeValue> item)
    {
        var customer = FromItem(item);

        if (item.TryGetValue(nameof(CustomerItem.ReferenceIds), out var referenceIds)
            && referenceIds.NULL != true
            && referenceIds.SS != null)
            customer.ReferenceIds = referenceIds.SS.Select(Guid.Parse).ToHashSet();

        return customer;
    }
}

[DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullValues = false)]
internal static partial class SaveChangesOrderItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(OrderItem source);

    internal static partial OrderItem FromItem(Dictionary<string, AttributeValue> item);

    internal static OrderItem ToOrderItem(this Dictionary<string, AttributeValue> item)
        => FromItem(item);
}

[DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullValues = false)]
internal static partial class SaveChangesProductItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(ProductItem source);

    internal static partial ProductItem FromItem(Dictionary<string, AttributeValue> item);
}

[DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullValues = false)]
internal static partial class SaveChangesSessionItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(SessionItem source);

    internal static partial SessionItem FromItem(Dictionary<string, AttributeValue> item);
}

[DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullValues = false)]
[DynamoField(nameof(ConverterCoverageItem.OccurredAt), Format = "yyyy-MM-dd HH:mm:sszzz")]
[DynamoField(nameof(ConverterCoverageItem.History), Format = "yyyy-MM-dd HH:mm:sszzz")]
internal static partial class SaveChangesConverterCoverageItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(ConverterCoverageItem source);

    internal static partial ConverterCoverageItem FromItem(Dictionary<string, AttributeValue> item);

    internal static ConverterCoverageItem ToConverterCoverageItem(
        this Dictionary<string, AttributeValue> item)
        => FromItem(item);
}
