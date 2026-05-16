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

[DynamoMapper(
    Convention = DynamoNamingConvention.CamelCase,
    OmitNullValues = false,
    DateTimeFormat = "yyyy-MM-dd HH:mm:sszzz")]
internal partial class SaveChangesOrderItemMapper : IDynamoMapper<OrderItem>
{
    public static partial Dictionary<string, AttributeValue> ToItem(OrderItem source);

    public static partial OrderItem FromItem(Dictionary<string, AttributeValue> item);
}

[DynamoMapper(
    Convention = DynamoNamingConvention.CamelCase,
    OmitNullValues = false,
    DateTimeFormat = "yyyy-MM-dd HH:mm:sszzz")]
internal partial class SaveChangesProductItemMapper : IDynamoMapper<ProductItem>
{
    public static partial Dictionary<string, AttributeValue> ToItem(ProductItem source);

    public static partial ProductItem FromItem(Dictionary<string, AttributeValue> item);
}

[DynamoMapper(
    Convention = DynamoNamingConvention.CamelCase,
    OmitNullValues = false,
    DateTimeFormat = "yyyy-MM-dd HH:mm:sszzz")]
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

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
[DynamoField(
    nameof(CustomConverterItem.Code),
    ToMethod = nameof(ToProductCode),
    FromMethod = nameof(FromProductCode))]
[DynamoField(
    nameof(CustomConverterItem.OptionalCode),
    ToMethod = nameof(ToNullableProductCode),
    FromMethod = nameof(FromNullableProductCode))]
internal partial class SaveChangesCustomConverterItemMapper : IDynamoMapper<CustomConverterItem>
{
    public static partial Dictionary<string, AttributeValue> ToItem(CustomConverterItem source);

    public static partial CustomConverterItem FromItem(Dictionary<string, AttributeValue> item);

    private static AttributeValue ToProductCode(CustomConverterItem source)
        => source.Code.Value.ToAttributeValue();

    private static ProductCode FromProductCode(Dictionary<string, AttributeValue> item)
        => new(item["code"].S);

    private static AttributeValue ToNullableProductCode(CustomConverterItem source)
        => source.OptionalCode.HasValue
            ? source.OptionalCode.Value.Value.ToAttributeValue()
            : new AttributeValue { NULL = true };

    private static ProductCode? FromNullableProductCode(Dictionary<string, AttributeValue> item)
        => item["optionalCode"].NULL == true ? null : new ProductCode(item["optionalCode"].S);
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
[DynamoField(
    nameof(ConvertedCollectionItem.Scores),
    ToMethod = nameof(ToScores),
    FromMethod = nameof(FromScores))]
internal partial class SaveChangesConvertedCollectionItemMapper
    : IDynamoMapper<ConvertedCollectionItem>
{
    public static partial Dictionary<string, AttributeValue> ToItem(ConvertedCollectionItem source);

    public static partial ConvertedCollectionItem FromItem(Dictionary<string, AttributeValue> item);

    private static AttributeValue ToScores(ConvertedCollectionItem source)
        => string.Join('|', source.Scores).ToAttributeValue();

    private static List<int> FromScores(Dictionary<string, AttributeValue> item)
        => string.IsNullOrWhiteSpace(item["scores"].S)
            ? []
            : item["scores"]
                .S
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList();
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
[DynamoField(nameof(QuotedAttributeItem.DisplayName), AttributeName = "O'Brien")]
internal partial class SaveChangesQuotedAttributeItemMapper : IDynamoMapper<QuotedAttributeItem>
{
    public static partial Dictionary<string, AttributeValue> ToItem(QuotedAttributeItem source);

    public static partial QuotedAttributeItem FromItem(Dictionary<string, AttributeValue> item);
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
[DynamoField(nameof(SparseGsiItem.Gs1Pk), AttributeName = "gs1-pk")]
[DynamoField(nameof(SparseGsiItem.Gs1Sk), AttributeName = "gs1-sk")]
internal partial class SaveChangesSparseGsiItemMapper : IDynamoMapper<SparseGsiItem>
{
    public static partial Dictionary<string, AttributeValue> ToItem(SparseGsiItem source);

    public static partial SparseGsiItem FromItem(Dictionary<string, AttributeValue> item);
}
