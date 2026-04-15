using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SaveChangesTable;

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
            ReferenceIds =
            [
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            ],
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
            ReferenceIds = [],
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
            .Select(item => WithDiscriminator(
                SaveChangesCustomerItemMapper.ToItem(item),
                nameof(CustomerItem)))
            .Concat(
                Orders.Select(item => WithDiscriminator(
                    SaveChangesOrderItemMapper.ToItem(item),
                    nameof(OrderItem))))
            .Concat(
                Products.Select(item => WithDiscriminator(
                    SaveChangesProductItemMapper.ToItem(item),
                    nameof(ProductItem))))
            .Concat(
                Sessions.Select(item => WithDiscriminator(
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
