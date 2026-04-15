using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventionTable;

/// <summary>
///     Source-generated mapper for <see cref="NamingConventionItem" />. Uses
///     <c>DynamoNamingConvention.SnakeCase</c> so that seed attribute names in DynamoDB match the
///     snake_case names that the EF provider will emit in PartiQL. The <c>ExplicitOverride</c>
///     property uses a <c>[DynamoField]</c> override to align with the explicit
///     <c>HasAttributeName("custom_attr")</c> configured in the DbContext.
/// </summary>
[DynamoMapper(Convention = DynamoNamingConvention.SnakeCase, OmitNullValues = false)]
[DynamoField("ExplicitOverride", AttributeName = "custom_attr")]
internal static partial class NamingConventionItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(NamingConventionItem source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(
        List<NamingConventionItem> sources)
        => sources.Select(ToItem).ToList();

    internal static partial NamingConventionItem FromItem(Dictionary<string, AttributeValue> item);

    internal static List<NamingConventionItem> FromItems(
        List<Dictionary<string, AttributeValue>> items)
        => items.Select(FromItem).ToList();
}
