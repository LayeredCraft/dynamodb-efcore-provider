namespace EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

/// <summary>Represents the OwnedShapeItem type.</summary>
public sealed record OwnedShapeItem
{
    public DateTimeOffset CreatedAt { get; set; }

    public Guid GuidValue { get; set; }

    public int IntValue { get; set; }

    public List<Order> Orders { get; set; } = [];

    public List<OrderSnapshot> OrderSnapshots { get; set; } = [];

    public string Pk { get; set; } = null!;

    public Profile? Profile { get; set; }

    public List<int> Ratings { get; set; } = [];

    public string StringValue { get; set; } = null!;

    public List<string> Tags { get; set; } = [];

    // public Dictionary<string, ContactMethod> ContactsByType { get; set; } = [];
}

/// <summary>Represents the Profile type.</summary>
public sealed record Profile
{
    public Address? Address { get; set; }

    public int? Age { get; set; }

    public string DisplayName { get; set; } = null!;

    // public Dictionary<string, Preference> PreferencesByKey { get; set; } = [];
}

/// <summary>Represents the Address type.</summary>
public sealed record Address
{
    public string City { get; set; } = null!;

    public Geo? Geo { get; set; }

    public string Line1 { get; set; } = null!;
}

/// <summary>Represents the Geo type.</summary>
public sealed record Geo
{
    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }
}

/// <summary>Represents the Order type.</summary>
public sealed record Order
{
    public List<OrderLine> Lines { get; set; } = [];

    public string OrderNumber { get; set; } = null!;

    public Payment? Payment { get; set; }

    public decimal Total { get; set; }
}

/// <summary>Represents the Payment type.</summary>
public sealed record Payment
{
    public Card? Card { get; set; }

    public string Provider { get; set; } = null!;
}

/// <summary>Represents the Card type.</summary>
public sealed record Card
{
    public int ExpMonth { get; set; }

    public int ExpYear { get; set; }

    public string Last4 { get; set; } = null!;
}

/// <summary>Represents the OrderLine type.</summary>
public sealed record OrderLine
{
    public int Quantity { get; set; }

    public string Sku { get; set; } = null!;
}

/// <summary>Represents the OrderSnapshot type.</summary>
public sealed record OrderSnapshot
{
    public string SnapshotNumber { get; set; } = null!;

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
