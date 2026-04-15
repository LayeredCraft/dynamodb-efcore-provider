using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>Represents the CustomerItem type.</summary>
public sealed record CustomerItem
{
    /// <summary>Provides functionality for this member.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Sk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public long Version { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public string Email { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public bool IsPreferred { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public CustomerProfile? Profile { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public List<CustomerContact> Contacts { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public Dictionary<string, string> Preferences { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public HashSet<string> Tags { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public HashSet<Guid> ReferenceIds { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public List<string> Notes { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public string? NullableNote { get; set; }
}

/// <summary>Represents the OrderItem type.</summary>
public sealed record OrderItem
{
    /// <summary>Provides functionality for this member.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Sk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public long Version { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public string CustomerPk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Status { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public decimal Total { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public ShippingDetails? Shipping { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public BillingDetails? Billing { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public List<OrderLine> Lines { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public List<string> AppliedCoupons { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public Dictionary<string, decimal> ChargesByCode { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public HashSet<int> RiskFlags { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public string? CancellationReason { get; set; }
}

/// <summary>Represents the ProductItem type.</summary>
public sealed record ProductItem
{
    /// <summary>Provides functionality for this member.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Sk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public long Version { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public decimal Price { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public bool IsActive { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public ProductDimensions? Dimensions { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public List<ProductVariant> Variants { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public HashSet<string> CategorySet { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public List<string> SearchTerms { get; set; } = [];
}

/// <summary>Represents the SessionItem type.</summary>
public sealed record SessionItem
{
    /// <summary>Provides functionality for this member.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Sk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public long Version { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public string CustomerPk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public SessionDevice? Device { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public List<string> Scopes { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public Dictionary<string, string> Attributes { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public HashSet<string> Flags { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public bool Revoked { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public DateTimeOffset? LastSeenAt { get; set; }
}

/// <summary>Represents the ConverterCoverageItem type.</summary>
public sealed record ConverterCoverageItem
{
    /// <summary>Provides functionality for this member.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Sk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public long Version { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public Guid ExternalId { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public byte[] Payload { get; set; } = [];

    /// <summary>Provides functionality for this member.</summary>
    public HashSet<byte[]>? BinaryTags { get; set; }

    /// <summary>Provides functionality for this member.</summary>
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
    /// <summary>Provides functionality for this member.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
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
    /// <summary>Provides functionality for this member.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Sk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public long Version { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public List<int> Scores { get; set; } = [];
}

/// <summary>Entity used to validate escaping for custom DynamoDB attribute names.</summary>
public sealed record QuotedAttributeItem
{
    /// <summary>Provides functionality for this member.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Sk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public long Version { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public string DisplayName { get; set; } = null!;
}

/// <summary>Represents the CustomerProfile type.</summary>
public sealed record CustomerProfile
{
    /// <summary>Provides functionality for this member.</summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string? Nickname { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public Address? PreferredAddress { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public Address? BillingAddress { get; set; }
}

/// <summary>Represents the CustomerContact type.</summary>
public sealed record CustomerContact
{
    /// <summary>Provides functionality for this member.</summary>
    public string Kind { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Value { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public bool Verified { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public DateTimeOffset? VerifiedAt { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public Address? Address { get; set; }
}

/// <summary>Represents the ShippingDetails type.</summary>
public sealed record ShippingDetails
{
    /// <summary>Provides functionality for this member.</summary>
    public string Method { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public Address? Address { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public DeliveryWindow? DeliveryWindow { get; set; }
}

/// <summary>Represents the BillingDetails type.</summary>
public sealed record BillingDetails
{
    /// <summary>Provides functionality for this member.</summary>
    public bool SameAsShipping { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public Address? Address { get; set; }
}

/// <summary>Represents the OrderLine type.</summary>
public sealed record OrderLine
{
    /// <summary>Provides functionality for this member.</summary>
    public string Sku { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public int Quantity { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public Dictionary<string, string> Metadata { get; set; } = [];
}

/// <summary>Represents the DeliveryWindow type.</summary>
public sealed record DeliveryWindow
{
    /// <summary>Provides functionality for this member.</summary>
    public DateTimeOffset Start { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public DateTimeOffset End { get; set; }
}

/// <summary>Represents the ProductDimensions type.</summary>
public sealed record ProductDimensions
{
    /// <summary>Provides functionality for this member.</summary>
    public decimal Height { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public decimal Width { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public decimal Depth { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public decimal? Weight { get; set; }
}

/// <summary>Represents the ProductVariant type.</summary>
public sealed record ProductVariant
{
    /// <summary>Provides functionality for this member.</summary>
    public string Code { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Color { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public bool Backordered { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public List<string> AlternateCodes { get; set; } = [];
}

/// <summary>Represents the SessionDevice type.</summary>
public sealed record SessionDevice
{
    /// <summary>Provides functionality for this member.</summary>
    public string Platform { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string UserAgent { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public Address? LastKnownAddress { get; set; }
}

/// <summary>Represents the Address type.</summary>
public sealed record Address
{
    /// <summary>Provides functionality for this member.</summary>
    public string Line1 { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string City { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Country { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string? PostalCode { get; set; }
}
