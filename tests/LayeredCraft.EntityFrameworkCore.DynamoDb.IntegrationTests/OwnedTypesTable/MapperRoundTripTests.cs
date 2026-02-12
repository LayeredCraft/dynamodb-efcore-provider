using System.Text.Json;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

public class MapperRoundTripTests
{
    [Fact]
    public void ToItem_FromItem_round_trips_owned_shapes()
    {
        var item = OwnedTypesItems.Items.First(x => x.Pk == "OWNED#1");
        var map = OwnedTypesItemMapper.ToItem(item);

        map.Should().ContainKey("Profile");
        map.Should().ContainKey("Orders");
        map.Should().ContainKey("OrderSnapshots");

        var roundTrip = OwnedTypesItemMapper.FromItem(map);

        roundTrip.Profile.Should().NotBeNull();
        roundTrip.Orders.Should().NotBeNull();
        roundTrip.Orders.Should().NotBeEmpty();
        roundTrip.OrderSnapshots.Should().NotBeNull();
        roundTrip.OrderSnapshots.Should().NotBeEmpty();
    }

    [Fact]
    public void Profile_attribute_deserializes_from_attribute_value_json_wrapper()
    {
        var item = OwnedTypesItems.Items.First(x => x.Pk == "OWNED#1");
        var map = OwnedTypesItemMapper.ToItem(item);

        var wrapper =
            new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["value"] = map["Profile"],
            };

        var document = Document.FromAttributeMap(wrapper);
        using var jsonDocument = JsonDocument.Parse(document.ToJson());
        var raw = jsonDocument.RootElement.GetProperty("value").GetRawText();
        var profile = JsonSerializer.Deserialize<Profile>(raw);

        profile.Should().NotBeNull();
        profile!.DisplayName.Should().Be("Ada");
    }
}
