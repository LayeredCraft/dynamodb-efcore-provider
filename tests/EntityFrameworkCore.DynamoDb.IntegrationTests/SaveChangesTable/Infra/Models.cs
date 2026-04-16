using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>Represents the CustomerItem type.</summary>
public sealed record CustomerItem
{
    public string Pk { get; set; } = null!;

    public string Sk { get; set; } = null!;

    public long Version { get; set; }

    public string Email { get; set; } = null!;

    public bool IsPreferred { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public CustomerProfile? Profile { get; set; }

    public List<CustomerContact> Contacts { get; set; } = [];

    public Dictionary<string, string> Preferences { get; set; } = [];

    public HashSet<string> Tags { get; set; } = [];

    public HashSet<Guid> ReferenceIds { get; set; } = [];

    public List<string> Notes { get; set; } = [];

    public string? NullableNote { get; set; }
}

/// <summary>Represents the OrderItem type.</summary>
public sealed record OrderItem
{
    public string Pk { get; set; } = null!;

    public string Sk { get; set; } = null!;

    public long Version { get; set; }

    public string CustomerPk { get; set; } = null!;

    public string Status { get; set; } = null!;

    public decimal Total { get; set; }

    public ShippingDetails? Shipping { get; set; }

    public BillingDetails? Billing { get; set; }

    public List<OrderLine> Lines { get; set; } = [];

    public List<string> AppliedCoupons { get; set; } = [];

    public Dictionary<string, decimal> ChargesByCode { get; set; } = [];

    public HashSet<int> RiskFlags { get; set; } = [];

    public string? CancellationReason { get; set; }
}

/// <summary>Represents the ProductItem type.</summary>
public sealed record ProductItem
{
    public string Pk { get; set; } = null!;

    public string Sk { get; set; } = null!;

    public long Version { get; set; }

    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public ProductDimensions? Dimensions { get; set; }

    public List<ProductVariant> Variants { get; set; } = [];

    public Dictionary<string, string> Metadata { get; set; } = [];

    public HashSet<string> CategorySet { get; set; } = [];

    public List<string> SearchTerms { get; set; } = [];
}

/// <summary>Represents the SessionItem type.</summary>
public sealed record SessionItem
{
    public string Pk { get; set; } = null!;

    public string Sk { get; set; } = null!;

    public long Version { get; set; }

    public string CustomerPk { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    public SessionDevice? Device { get; set; }

    public List<string> Scopes { get; set; } = [];

    public Dictionary<string, string> Attributes { get; set; } = [];

    public HashSet<string> Flags { get; set; } = [];

    public bool Revoked { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }
}

/// <summary>Represents the ConverterCoverageItem type.</summary>
public sealed record ConverterCoverageItem
{
    public string Pk { get; set; } = null!;

    public string Sk { get; set; } = null!;

    public long Version { get; set; }

    public Guid ExternalId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public byte[] Payload { get; set; } = [];

    public HashSet<byte[]>? BinaryTags { get; set; }

    public List<DateTimeOffset> History { get; set; } = [];
}

/// <summary>
///     A user-defined value object that is not in the provider's type-dispatch table. Properties
///     of this type must use a value converter, and will exercise the boxed fallback write path in
///     <c>DynamoEntityItemSerializerSource</c>.
/// </summary>
public readonly record struct ProductCode(string Value);

/// <summary>
///     Converts <see cref="ProductCode" /> to and from its <c>string</c> wire representation.
///     Registered as a custom converter in <see cref="SaveChangesTableDbContext" /> to exercise the
///     boxed fallback serialization path for non-dispatch-table model types.
/// </summary>
public sealed class ProductCodeConverter() : ValueConverter<ProductCode, string>(
    code => code.Value,
    str => new ProductCode(str));

/// <summary>
///     An entity that uses a custom <see cref="ProductCode" /> value type via a user-supplied
///     converter.
/// </summary>
public sealed record CustomConverterItem
{
    public string Pk { get; set; } = null!;

    public string Sk { get; set; } = null!;

    /// <summary>Required custom-type property — exercises the boxed scalar fallback (direct match path).</summary>
    public ProductCode Code { get; set; }

    /// <summary>
    ///     Optional custom-type property — exercises the boxed scalar fallback (nullable wrapping
    ///     path).
    /// </summary>
    public ProductCode? OptionalCode { get; set; }
}

/// <summary>Entity used to validate property-level conversion of a collection-shaped CLR property.</summary>
public sealed record ConvertedCollectionItem
{
    public string Pk { get; set; } = null!;

    public string Sk { get; set; } = null!;

    public long Version { get; set; }

    public List<int> Scores { get; set; } = [];
}

/// <summary>Entity used to validate escaping for custom DynamoDB attribute names.</summary>
public sealed record QuotedAttributeItem
{
    public string Pk { get; set; } = null!;

    public string Sk { get; set; } = null!;

    public long Version { get; set; }

    public string DisplayName { get; set; } = null!;
}

/// <summary>Represents the CustomerProfile type.</summary>
public sealed record CustomerProfile
{
    public string DisplayName { get; set; } = null!;

    public string? Nickname { get; set; }

    public Address? PreferredAddress { get; set; }

    public Address? BillingAddress { get; set; }
}

/// <summary>Represents the CustomerContact type.</summary>
public sealed record CustomerContact
{
    public string Kind { get; set; } = null!;

    public string Value { get; set; } = null!;

    public bool Verified { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }

    public Address? Address { get; set; }
}

/// <summary>Represents the ShippingDetails type.</summary>
public sealed record ShippingDetails
{
    public string Method { get; set; } = null!;

    public Address? Address { get; set; }

    public DeliveryWindow? DeliveryWindow { get; set; }
}

/// <summary>Represents the BillingDetails type.</summary>
public sealed record BillingDetails
{
    public bool SameAsShipping { get; set; }

    public Address? Address { get; set; }
}

/// <summary>Represents the OrderLine type.</summary>
public sealed record OrderLine
{
    public string Sku { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public Dictionary<string, string> Metadata { get; set; } = [];
}

/// <summary>Represents the DeliveryWindow type.</summary>
public sealed record DeliveryWindow
{
    public DateTimeOffset Start { get; set; }

    public DateTimeOffset End { get; set; }
}

/// <summary>Represents the ProductDimensions type.</summary>
public sealed record ProductDimensions
{
    public decimal Height { get; set; }

    public decimal Width { get; set; }

    public decimal Depth { get; set; }

    public decimal? Weight { get; set; }
}

/// <summary>Represents the ProductVariant type.</summary>
public sealed record ProductVariant
{
    public string Code { get; set; } = null!;

    public string Color { get; set; } = null!;

    public bool Backordered { get; set; }

    public List<string> AlternateCodes { get; set; } = [];
}

/// <summary>Represents the SessionDevice type.</summary>
public sealed record SessionDevice
{
    public string Platform { get; set; } = null!;

    public string UserAgent { get; set; } = null!;

    public Address? LastKnownAddress { get; set; }
}

/// <summary>Represents the Address type.</summary>
public sealed record Address
{
    public string Line1 { get; set; } = null!;

    public string City { get; set; } = null!;

    public string Country { get; set; } = null!;

    public string? PostalCode { get; set; }
}
