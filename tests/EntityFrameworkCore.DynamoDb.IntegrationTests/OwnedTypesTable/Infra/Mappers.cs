using System.Text.Json;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

// [DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullStrings = false)]
// internal static partial class OwnedTypesItemMapper
// {
//     /// <summary>Maps a CLR test item to a DynamoDB item payload.</summary>
//     internal static partial Dictionary<string, AttributeValue> ToItem(OwnedShapeItem source);
//
//     /// <summary>Maps CLR test items to DynamoDB item payloads.</summary>
//     internal static List<Dictionary<string, AttributeValue>> ToItems(List<OwnedShapeItem>
// sources)
//         => sources.Select(ToItem).ToList();
// }

internal static class OwnedTypesItemMapper
{
    /// <summary>Maps a CLR test item to a DynamoDB item payload.</summary>
    internal static Dictionary<string, AttributeValue> ToItem(OwnedShapeItem source)
    {
        var json = JsonSerializer.Serialize(source);
        return Document.FromJson(json).ToAttributeMap();
    }

    /// <summary>Maps CLR test items to DynamoDB item payloads.</summary>
    internal static List<Dictionary<string, AttributeValue>> ToItems(List<OwnedShapeItem> sources)
        => sources.Select(ToItem).ToList();

    /// <summary>Maps a DynamoDB item payload back to a CLR test item.</summary>
    internal static OwnedShapeItem FromItem(Dictionary<string, AttributeValue> item)
        => JsonSerializer.Deserialize<OwnedShapeItem>(Document.FromAttributeMap(item).ToJson())!;

    /// <summary>Maps DynamoDB item payloads back to CLR test items.</summary>
    internal static List<OwnedShapeItem> FromItems(List<Dictionary<string, AttributeValue>> items)
        => items.Select(FromItem).ToList();
}
