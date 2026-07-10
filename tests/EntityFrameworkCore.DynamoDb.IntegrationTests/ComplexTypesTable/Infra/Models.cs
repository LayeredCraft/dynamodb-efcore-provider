namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ComplexTypesTable;

public sealed record ComplexShapeItem
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
}

public sealed record Profile
{
    public Address? Address { get; set; }

    public int? Age { get; set; }

    public string DisplayName { get; set; } = null!;
}

public sealed record Address
{
    public string City { get; set; } = null!;

    public Geo? Geo { get; set; }

    public string Line1 { get; set; } = null!;
}

public sealed record Geo
{
    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }
}

public sealed record Order
{
    public List<OrderLine> Lines { get; set; } = [];

    public string OrderNumber { get; set; } = null!;

    public Payment? Payment { get; set; }

    public decimal Total { get; set; }
}

public sealed record Payment
{
    public Card? Card { get; set; }

    public string Provider { get; set; } = null!;
}

public sealed record Card
{
    public int ExpMonth { get; set; }

    public int ExpYear { get; set; }

    public string Last4 { get; set; } = null!;
}

public sealed record OrderLine
{
    public int Quantity { get; set; }

    public string Sku { get; set; } = null!;
}

public sealed record OrderSnapshot
{
    public string SnapshotNumber { get; set; } = null!;

    public decimal Total { get; set; }
}

/// <summary>Root entity that owns a collection of <see cref="ScoredResult"/> elements.</summary>
public sealed record AnalysisReport
{
    public string Pk { get; set; } = null!;

    public List<ScoredResult> Results { get; set; } = [];
}

/// <summary>
///     Complex collection element with a CLR property named <c>Id</c> — mirrors the real-world
///     scenario that triggered provider table-key resolution replacing EF Core key discovery.
/// </summary>
public sealed record ScoredResult
{
    public string Id { get; set; } = null!;

    public float Score { get; set; }
}

/// <summary>
///     Root entity used to verify convention-only support for advertised complex collection
///     shapes.
/// </summary>
public sealed record CollectionShapeItem
{
    public string Pk { get; set; } = null!;

    public IList<ShapeContact> InterfaceContacts { get; set; } = [];

    public List<ShapeContact> MutableContacts { get; set; } = [];
}

/// <summary>Complex collection element used by collection-shape convention tests.</summary>
public sealed record ShapeContact
{
    public string Label { get; set; } = null!;

    public ShapeAddress? Address { get; set; }
}

/// <summary>Nested complex type under <see cref="ShapeContact" />.</summary>
public sealed record ShapeAddress
{
    public string City { get; set; } = null!;
}
