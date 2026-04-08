using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

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

/// <summary>Represents the SaveChangesTableItems type.</summary>
public static class SaveChangesTableItems
{
    /// <summary>Provides functionality for this member.</summary>
    public static readonly List<CustomerItem> Customers =
    [
        new()
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#CUST-1",
            Version = 3,
            Email = "ada@example.com",
            IsPreferred = true,
            CreatedAt = new DateTimeOffset(2026, 02, 01, 09, 00, 00, TimeSpan.Zero),
            NullableNote = "priority account",
            Preferences = new Dictionary<string, string> { ["language"] = "en", ["tier"] = "gold" },
            Tags = ["vip", "beta"],
            Notes = ["seeded", "newsletter"],
            Profile =
                new CustomerProfile
                {
                    DisplayName = "Ada Lovelace",
                    Nickname = "ada",
                    PreferredAddress =
                        new Address
                        {
                            Line1 = "10 Main St",
                            City = "Seattle",
                            Country = "US",
                            PostalCode = "98101",
                        },
                    BillingAddress =
                        new Address
                        {
                            Line1 = "11 Billing Ave",
                            City = "Seattle",
                            Country = "US",
                            PostalCode = "98102",
                        },
                },
            Contacts =
            [
                new CustomerContact
                {
                    Kind = "email",
                    Value = "ada@example.com",
                    Verified = true,
                    VerifiedAt =
                        new DateTimeOffset(2026, 02, 02, 10, 30, 00, TimeSpan.Zero),
                    Address = null,
                },
                new CustomerContact
                {
                    Kind = "phone",
                    Value = "+12065550123",
                    Verified = false,
                    VerifiedAt = null,
                    Address =
                        new Address
                        {
                            Line1 = "Mobile",
                            City = "Seattle",
                            Country = "US",
                            PostalCode = null,
                        },
                },
            ],
        },
        new()
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#CUST-2",
            Version = 1,
            Email = "lin@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 02, 05, 14, 15, 00, TimeSpan.Zero),
            NullableNote = null,
            Preferences = [],
            Tags = ["standard"],
            Notes = [],
            Profile = null,
            Contacts = [],
        },
    ];

    /// <summary>Provides functionality for this member.</summary>
    public static readonly List<OrderItem> Orders =
    [
        new()
        {
            Pk = "TENANT#1",
            Sk = "ORDER#ORD-100",
            Version = 7,
            CustomerPk = "CUSTOMER#CUST-1",
            Status = "Placed",
            Total = 126.50m,
            CancellationReason = null,
            AppliedCoupons = ["WELCOME10"],
            ChargesByCode =
                new Dictionary<string, decimal> { ["shipping"] = 9.99m, ["tax"] = 11.51m },
            RiskFlags = [1, 4],
            Shipping =
                new ShippingDetails
                {
                    Method = "Ground",
                    Address =
                        new Address
                        {
                            Line1 = "10 Main St",
                            City = "Seattle",
                            Country = "US",
                            PostalCode = "98101",
                        },
                    DeliveryWindow =
                        new DeliveryWindow
                        {
                            Start = new DateTimeOffset(
                                2026,
                                02,
                                10,
                                09,
                                00,
                                00,
                                TimeSpan.Zero),
                            End =
                                new DateTimeOffset(
                                    2026,
                                    02,
                                    10,
                                    17,
                                    00,
                                    00,
                                    TimeSpan.Zero),
                        },
                },
            Billing =
                new BillingDetails
                {
                    SameAsShipping = false,
                    Address =
                        new Address
                        {
                            Line1 = "11 Billing Ave",
                            City = "Seattle",
                            Country = "US",
                            PostalCode = "98102",
                        },
                },
            Lines =
            [
                new OrderLine
                {
                    Sku = "SKU-1",
                    Quantity = 2,
                    UnitPrice = 25.00m,
                    Metadata =
                        new Dictionary<string, string> { ["warehouse"] = "A1" },
                },
                new OrderLine
                {
                    Sku = "SKU-2",
                    Quantity = 1,
                    UnitPrice = 80.00m,
                    Metadata =
                        new Dictionary<string, string> { ["warehouse"] = "B4" },
                },
            ],
        },
        new()
        {
            Pk = "TENANT#1",
            Sk = "ORDER#ORD-101",
            Version = 2,
            CustomerPk = "CUSTOMER#CUST-2",
            Status = "Cancelled",
            Total = 0.00m,
            CancellationReason = "customer-request",
            AppliedCoupons = [],
            ChargesByCode = [],
            RiskFlags = [],
            Shipping = null,
            Billing = null,
            Lines = [],
        },
    ];

    /// <summary>Provides functionality for this member.</summary>
    public static readonly List<ProductItem> Products =
    [
        new()
        {
            Pk = "TENANT#1",
            Sk = "PRODUCT#PROD-1",
            Version = 5,
            Name = "Widget",
            Price = 19.95m,
            IsActive = true,
            PublishedAt = new DateTimeOffset(2026, 01, 15, 08, 00, 00, TimeSpan.Zero),
            CategorySet = ["tools", "featured"],
            SearchTerms = ["widget", "starter"],
            Metadata = new Dictionary<string, string> { ["brand"] = "Acme", ["region"] = "us" },
            Dimensions =
                new ProductDimensions
                {
                    Height = 10.5m, Width = 3.2m, Depth = 1.1m, Weight = 0.9m,
                },
            Variants =
            [
                new ProductVariant
                {
                    Code = "WIDGET-BLACK",
                    Color = "black",
                    Backordered = false,
                    AlternateCodes = ["WBK-1", "WBK-2"],
                },
                new ProductVariant
                {
                    Code = "WIDGET-RED",
                    Color = "red",
                    Backordered = true,
                    AlternateCodes = [],
                },
            ],
        },
        new()
        {
            Pk = "TENANT#1",
            Sk = "PRODUCT#PROD-2",
            Version = 1,
            Name = "Legacy Part",
            Price = 3.50m,
            IsActive = false,
            PublishedAt = null,
            CategorySet = ["clearance"],
            SearchTerms = [],
            Metadata = [],
            Dimensions = null,
            Variants = [],
        },
    ];

    /// <summary>Provides functionality for this member.</summary>
    public static readonly List<SessionItem> Sessions =
    [
        new()
        {
            Pk = "TENANT#1",
            Sk = "SESSION#SES-1",
            Version = 4,
            CustomerPk = "CUSTOMER#CUST-1",
            ExpiresAt = new DateTimeOffset(2026, 03, 01, 00, 00, 00, TimeSpan.Zero),
            Revoked = false,
            LastSeenAt = new DateTimeOffset(2026, 02, 20, 12, 45, 00, TimeSpan.Zero),
            Scopes = ["cart:read", "orders:write"],
            Attributes =
                new Dictionary<string, string>
                {
                    ["ip"] = "203.0.113.42", ["deviceId"] = "dev-1",
                },
            Flags = ["mfa"],
            Device = new SessionDevice
            {
                Platform = "ios",
                UserAgent = "MobileSafari/17",
                LastKnownAddress =
                    new Address
                    {
                        Line1 = "Roaming",
                        City = "Portland",
                        Country = "US",
                        PostalCode = null,
                    },
            },
        },
        new()
        {
            Pk = "TENANT#1",
            Sk = "SESSION#SES-2",
            Version = 1,
            CustomerPk = "CUSTOMER#CUST-2",
            ExpiresAt = new DateTimeOffset(2026, 03, 05, 00, 00, 00, TimeSpan.Zero),
            Revoked = true,
            LastSeenAt = null,
            Scopes = ["profile:read"],
            Attributes = [],
            Flags = [],
            Device = null,
        },
    ];

    /// <summary>Provides functionality for this member.</summary>
    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
        CreateAttributeValues();

    /// <summary>Builds the raw DynamoDB seed payloads for all shared-table entity types.</summary>
    private static IReadOnlyList<Dictionary<string, AttributeValue>> CreateAttributeValues()
        => Customers
            .Select(item
                => WithDiscriminator(
                    SaveChangesCustomerItemMapper.ToItem(item),
                    nameof(CustomerItem)))
            .Concat(
                Orders.Select(item
                    => WithDiscriminator(
                        SaveChangesOrderItemMapper.ToItem(item),
                        nameof(OrderItem))))
            .Concat(
                Products.Select(item
                    => WithDiscriminator(
                        SaveChangesProductItemMapper.ToItem(item),
                        nameof(ProductItem))))
            .Concat(
                Sessions.Select(item
                    => WithDiscriminator(
                        SaveChangesSessionItemMapper.ToItem(item),
                        nameof(SessionItem))))
            .ToList();

    /// <summary>Adds the shared-table discriminator expected by the provider conventions.</summary>
    private static Dictionary<string, AttributeValue> WithDiscriminator(
        Dictionary<string, AttributeValue> item,
        string discriminator)
    {
        item["$type"] = new AttributeValue { S = discriminator };
        return item;
    }
}

[DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullValues = false)]
[DynamoField(nameof(CustomerItem.CreatedAt), Format = "yyyy-MM-dd HH:mm:sszzz")]
[DynamoField(
    nameof(CustomerItem.Contacts) + "." + nameof(CustomerContact.VerifiedAt),
    Format = "yyyy-MM-dd HH:mm:sszzz")]
internal static partial class SaveChangesCustomerItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(CustomerItem source);

    internal static partial CustomerItem FromItem(Dictionary<string, AttributeValue> item);

    internal static CustomerItem ToCustomerItem(this Dictionary<string, AttributeValue> item)
        => FromItem(item);
}

[DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullValues = false)]
internal static partial class SaveChangesOrderItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(OrderItem source);

    internal static partial OrderItem FromItem(Dictionary<string, AttributeValue> item);

    internal static OrderItem ToOrderItem(this Dictionary<string, AttributeValue> item)
        => FromItem(item);
}

[DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullValues = false)]
internal static partial class SaveChangesProductItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(ProductItem source);

    internal static partial ProductItem FromItem(Dictionary<string, AttributeValue> item);
}

[DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullValues = false)]
internal static partial class SaveChangesSessionItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(SessionItem source);

    internal static partial SessionItem FromItem(Dictionary<string, AttributeValue> item);
}

[DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullValues = false)]
[DynamoField(nameof(ConverterCoverageItem.OccurredAt), Format = "yyyy-MM-dd HH:mm:sszzz")]
[DynamoField(nameof(ConverterCoverageItem.History), Format = "yyyy-MM-dd HH:mm:sszzz")]
internal static partial class SaveChangesConverterCoverageItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(ConverterCoverageItem source);

    internal static partial ConverterCoverageItem FromItem(Dictionary<string, AttributeValue> item);

    internal static ConverterCoverageItem ToConverterCoverageItem(
        this Dictionary<string, AttributeValue> item)
        => FromItem(item);
}
