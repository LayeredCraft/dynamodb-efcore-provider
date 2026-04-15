using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventionTable;

/// <summary>
///     Source-generated mapper for <see cref="SnakeCaseItem" />. Uses
///     <c>DynamoNamingConvention.SnakeCase</c> so seed attribute names match the snake_case names
///     emitted by the EF provider. The <c>ExplicitOverride</c> property uses a
///     <c>[DynamoField]</c> override to align with the explicit <c>HasAttributeName("custom_attr")</c>
///     in the DbContext.
/// </summary>
[DynamoMapper(Convention = DynamoNamingConvention.SnakeCase, OmitNullValues = false)]
[DynamoField("ExplicitOverride", AttributeName = "custom_attr")]
internal static partial class SnakeCaseItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(SnakeCaseItem source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(
        IEnumerable<SnakeCaseItem> sources)
        => sources.Select(ToItem).ToList();
}

/// <summary>
///     Source-generated mapper for <see cref="KebabCaseItem" />. Uses
///     <c>DynamoNamingConvention.KebabCase</c> so seed attribute names match the kebab-case names
///     emitted by the EF provider.
/// </summary>
[DynamoMapper(Convention = DynamoNamingConvention.KebabCase, OmitNullValues = false)]
internal static partial class KebabCaseItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(KebabCaseItem source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(
        IEnumerable<KebabCaseItem> sources)
        => sources.Select(ToItem).ToList();
}
