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
