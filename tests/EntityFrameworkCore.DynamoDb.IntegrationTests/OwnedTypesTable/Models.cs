using System.Text.Json;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

/// <summary>Represents the OwnedShapeItem type.</summary>
public sealed record OwnedShapeItem
{
    /// <summary>Provides functionality for this member.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public Guid GuidValue { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public int IntValue { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public List<Order> Orders { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public List<OrderSnapshot> OrderSnapshots { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public Profile? Profile { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public List<int> Ratings { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public string StringValue { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public List<string> Tags { get; set; } = [];

    // public Dictionary<string, ContactMethod> ContactsByType { get; set; } = [];
}

/// <summary>Represents the Profile type.</summary>
public sealed record Profile
{
    /// <summary>Provides functionality for this member.</summary>
    public Address? Address { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public int? Age { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public string DisplayName { get; set; } = null!;

    // public Dictionary<string, Preference> PreferencesByKey { get; set; } = [];
}

/// <summary>Represents the Address type.</summary>
public sealed record Address
{
    /// <summary>Provides functionality for this member.</summary>
    public string City { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public Geo? Geo { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public string Line1 { get; set; } = null!;
}

/// <summary>Represents the Geo type.</summary>
public sealed record Geo
{
    /// <summary>Provides functionality for this member.</summary>
    public decimal Latitude { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public decimal Longitude { get; set; }
}

/// <summary>Represents the Order type.</summary>
public sealed record Order
{
    /// <summary>Provides functionality for this member.</summary>
    public List<OrderLine> Lines { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public string OrderNumber { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public Payment? Payment { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public decimal Total { get; set; }
}

/// <summary>Represents the Payment type.</summary>
public sealed record Payment
{
    /// <summary>Provides functionality for this member.</summary>
    public Card? Card { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public string Provider { get; set; } = null!;
}

/// <summary>Represents the Card type.</summary>
public sealed record Card
{
    /// <summary>Provides functionality for this member.</summary>
    public int ExpMonth { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public int ExpYear { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public string Last4 { get; set; } = null!;
}

/// <summary>Represents the OrderLine type.</summary>
public sealed record OrderLine
{
    /// <summary>Provides functionality for this member.</summary>
    public int Quantity { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public string Sku { get; set; } = null!;
}

/// <summary>Represents the OrderSnapshot type.</summary>
public sealed record OrderSnapshot
{
    /// <summary>Provides functionality for this member.</summary>
    public string SnapshotNumber { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public decimal Total { get; set; }
}

// public sealed record ContactMethod
// {
//     public string Value { get; set; } = null!;
//
//     public bool Verified { get; set; }
//
//     public DateTimeOffset? VerifiedAt { get; set; }
//
//     public List<string> Notes { get; set; } = [];
// }

// public sealed record Preference
// {
//     public string Value { get; set; } = null!;
//
//     public int Priority { get; set; }
// }

/// <summary>Represents the OwnedTypesItems type.</summary>
public static class OwnedTypesItems
{
    /// <summary>Provides functionality for this member.</summary>
    public static readonly List<OwnedShapeItem> Items =
    [
        new()
        {
            Pk = "OWNED#1",
            IntValue = 11,
            StringValue = "alpha",
            GuidValue = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreatedAt = new DateTimeOffset(2026, 01, 01, 8, 30, 0, TimeSpan.Zero),
            Tags = ["featured", "vip"],
            Ratings = [5, 4, 5],
            Profile = new Profile
            {
                DisplayName = "Ada",
                Age = 39,
                Address = new Address
                {
                    Line1 = "10 Main St",
                    City = "Seattle",
                    Geo = new Geo { Latitude = 47.6062m, Longitude = -122.3321m },
                },
                // PreferencesByKey =
                // {
                //     ["currency"] = new Preference { Value = "USD", Priority = 1 },
                //     ["theme"] = new Preference { Value = "light", Priority = 2 },
                // },
            },
            Orders =
            [
                new Order
                {
                    OrderNumber = "A-100",
                    Total = 49.95m,
                    Payment =
                        new Payment
                        {
                            Provider = "stripe",
                            Card = new Card
                            {
                                Last4 = "4242", ExpMonth = 6, ExpYear = 2030,
                            },
                        },
                    Lines =
                    [
                        new OrderLine { Sku = "SKU-1", Quantity = 1 },
                        new OrderLine { Sku = "SKU-2", Quantity = 2 },
                    ],
                },
            ],
            OrderSnapshots = [new OrderSnapshot { SnapshotNumber = "SNAP-1", Total = 100.25m }],
            // ContactsByType =
            // {
            //     ["email"] = new ContactMethod
            //     {
            //         Value = "ada@example.com",
            //         Verified = true,
            //         VerifiedAt = new DateTimeOffset(2026, 01, 02, 10, 0, 0, TimeSpan.Zero),
            //         Notes = ["primary", "billing"],
            //     },
            //     ["sms"] = new ContactMethod
            //     {
            //         Value = "+12065550123", Verified = false, VerifiedAt = null, Notes = [],
            //     },
            // },
        },
        new()
        {
            Pk = "OWNED#2",
            IntValue = 22,
            StringValue = "beta",
            GuidValue = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            CreatedAt = new DateTimeOffset(2026, 01, 05, 9, 15, 0, TimeSpan.Zero),
            Tags = ["new"],
            Ratings = [],
            Profile = null,
            Orders = [],
            OrderSnapshots = [],
            // ContactsByType =
            // {
            //     ["email"] = new ContactMethod
            //     {
            //         Value = "beta@example.com", Verified = false, VerifiedAt = null, Notes =
            // ["pending"],
            //     },
            // },
        },
        new()
        {
            Pk = "OWNED#3",
            IntValue = 33,
            StringValue = "gamma",
            GuidValue = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            CreatedAt = new DateTimeOffset(2026, 01, 09, 18, 45, 0, TimeSpan.Zero),
            Tags = [],
            Ratings = [1, 2, 3],
            Profile = new Profile
            {
                DisplayName = "Gina", Age = null, Address = null,
                // PreferencesByKey =
                // {
                //     ["timezone"] = new Preference { Value = "UTC", Priority = 1 },
                // },
            },
            Orders =
            [
                new Order
                {
                    OrderNumber = "G-500",
                    Total = 7.50m,
                    Payment = null,
                    Lines = [new OrderLine { Sku = "SKU-G1", Quantity = 5 }],
                },
                new Order
                {
                    OrderNumber = "G-501",
                    Total = 18.25m,
                    Payment =
                        new Payment
                        {
                            Provider = "stripe",
                            Card =
                                new Card
                                {
                                    Last4 = "9999", ExpMonth = 12, ExpYear = 2031,
                                },
                        },
                    Lines =
                    [
                        new OrderLine { Sku = "SKU-G2", Quantity = 1 },
                        new OrderLine { Sku = "SKU-G3", Quantity = 1 },
                    ],
                },
            ],
            OrderSnapshots =
            [
                new OrderSnapshot { SnapshotNumber = "SNAP-2", Total = 1.25m },
                new OrderSnapshot { SnapshotNumber = "SNAP-3", Total = 2.50m },
            ],
            // ContactsByType =
            // {
            //     ["email"] = new ContactMethod
            //     {
            //         Value = "gamma@example.com",
            //         Verified = true,
            //         VerifiedAt = new DateTimeOffset(2026, 01, 10, 12, 0, 0, TimeSpan.Zero),
            //         Notes = ["secondary"],
            //     },
            //     ["phone"] = new ContactMethod
            //     {
            //         Value = "+12065550999",
            //         Verified = true,
            //         VerifiedAt = new DateTimeOffset(2026, 01, 10, 12, 5, 0, TimeSpan.Zero),
            //         Notes = [],
            //     },
            // },
        },
        new()
        {
            Pk = "OWNED#4",
            IntValue = 44,
            StringValue = "delta",
            GuidValue = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            CreatedAt = new DateTimeOffset(2026, 01, 12, 5, 0, 0, TimeSpan.Zero),
            Tags = ["edge", "nulls"],
            Ratings = [0],
            Profile = null,
            Orders =
            [
                new Order
                {
                    OrderNumber = "D-1",
                    Total = 0.01m,
                    Payment = null,
                    Lines = [new OrderLine { Sku = "SKU-D1", Quantity = 1 }],
                },
            ],
            OrderSnapshots = [],
            // ContactsByType =
            // {
            //     ["pager"] = new ContactMethod
            //     {
            //         Value = "555-0001", Verified = false, VerifiedAt = null, Notes = [],
            //     },
            // },
        },
    ];

    /// <summary>Provides functionality for this member.</summary>
    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
        CreateAttributeValues();

    /// <summary>Converts deterministic test items to DynamoDB attribute maps.</summary>
    private static IReadOnlyList<Dictionary<string, AttributeValue>> CreateAttributeValues()
        => OwnedTypesItemMapper.ToItems(Items);
}

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
