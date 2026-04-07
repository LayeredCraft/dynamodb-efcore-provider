using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>
/// Integration tests that verify the DynamoDB AttributeValue shapes written by
/// <c>SaveChangesAsync</c> for the full range of property shapes in the SaveChangesTable model:
/// scalars, owned references (OwnsOne → M), owned collections (OwnsMany → L of M), primitive
/// lists (L), string sets (SS), numeric sets (NS), and string-keyed maps (M).
/// Each test inserts via EF Core and then reads the raw item via the DynamoDB SDK to assert the
/// wire format rather than re-reading through the provider projection stack.
/// </summary>
public class WriteValueSerializationTests(SaveChangesTableDynamoFixture fixture)
    : SaveChangesTableTestBase(fixture)
{
    // ──────────────────────────────────────────────────────────────────────────────
    //  Scalar properties
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A minimal CustomerItem with only scalar properties (no owned objects, no collections)
    /// should produce correct S, N, BOOL, and NULL AttributeValue wire representations.
    /// </summary>
    [Fact]
    public async Task CustomerItem_ScalarsOnly_WritesCorrectAttributeValues()
    {
        var entity = new CustomerItem
        {
            Pk = "TEST#SC",
            Sk = "CUSTOMER#WRITE-1",
            Version = 7,
            Email = "scalar@test.com",
            IsPreferred = true,
            CreatedAt = new DateTimeOffset(2026, 03, 01, 12, 00, 00, TimeSpan.Zero),
            NullableNote = null,
            Profile = null,
        };

        Db.Customers.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);
        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'CreatedAt': ?, 'Email': ?, 'IsPreferred': ?, 'Notes': ?, 'NullableNote': ?, 'Preferences': ?, 'Tags': ?, 'Version': ?, 'Contacts': ?}
            """);

        var item = await GetItemAsync(entity.Pk, entity.Sk, CancellationToken);

        item.Should().NotBeNull();
        SaveChangesCustomerItemMapper.FromItem(item).Should().BeEquivalentTo(entity);
    }

    /// <summary>
    /// After SaveChanges the EF Core change tracker should reflect the entity as Unchanged.
    /// </summary>
    [Fact]
    public async Task CustomerItem_AfterSave_ChangeTrackerStateIsUnchanged()
    {
        var entity = new CustomerItem
        {
            Pk = "TEST#CT",
            Sk = "CUSTOMER#WRITE-CT",
            Version = 1,
            Email = "tracker@test.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 03, 02, 12, 00, 00, TimeSpan.Zero),
        };

        Db.Customers.Add(entity);

        Db.Entry(entity).State.Should().Be(EntityState.Added);

        await Db.SaveChangesAsync(CancellationToken);

        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'CreatedAt': ?, 'Email': ?, 'IsPreferred': ?, 'Notes': ?, 'NullableNote': ?, 'Preferences': ?, 'Tags': ?, 'Version': ?, 'Contacts': ?}
            """);

        Db.Entry(entity).State.Should().Be(EntityState.Unchanged);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  OwnsOne  →  M
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A non-null OwnsOne navigation should produce a nested map (M) attribute whose keys
    /// match the owned type's property names.
    /// </summary>
    [Fact]
    public async Task CustomerItem_WithOwnedProfile_SerializesProfileAsMap()
    {
        var entity = new CustomerItem
        {
            Pk = "TEST#P",
            Sk = "CUSTOMER#WRITE-2",
            Version = 1,
            Email = "profile@test.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 03, 03, 12, 00, 00, TimeSpan.Zero),
            Profile = new CustomerProfile
            {
                DisplayName = "Test User",
                Nickname = "tester",
                PreferredAddress = null,
                BillingAddress = null,
            },
        };

        Db.Customers.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);
        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'CreatedAt': ?, 'Email': ?, 'IsPreferred': ?, 'Notes': ?, 'NullableNote': ?, 'Preferences': ?, 'Tags': ?, 'Version': ?, 'Contacts': ?, 'Profile': ?}
            """);

        var item = await GetItemAsync("TEST#P", "CUSTOMER#WRITE-2", CancellationToken);
        item.Should().NotBeNull();

        item!.Should().ContainKey("Profile");
        SaveChangesCustomerItemMapper.FromItem(item).Should().BeEquivalentTo(entity);
    }

    /// <summary>
    /// A null OwnsOne navigation must not produce any attribute key in the item at all.
    /// </summary>
    [Fact]
    public async Task CustomerItem_WithNullOwnedProfile_OmitsProfileAttribute()
    {
        var entity = new CustomerItem
        {
            Pk = "TEST#NP",
            Sk = "CUSTOMER#WRITE-7",
            Version = 1,
            Email = "noprofile@test.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 03, 04, 12, 00, 00, TimeSpan.Zero),
            Profile = null,
        };

        Db.Customers.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);
        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'CreatedAt': ?, 'Email': ?, 'IsPreferred': ?, 'Notes': ?, 'NullableNote': ?, 'Preferences': ?, 'Tags': ?, 'Version': ?, 'Contacts': ?}
            """);

        var item = await GetItemAsync("TEST#NP", "CUSTOMER#WRITE-7", CancellationToken);
        item.Should().NotBeNull();
        item!.Should().NotContainKey("Profile");
        SaveChangesCustomerItemMapper.FromItem(item).Should().BeEquivalentTo(entity);
    }

    /// <summary>
    /// A deeply-nested OwnsOne chain (Profile → PreferredAddress → Address scalars) should
    /// produce nested maps at each level without losing any scalar values.
    /// </summary>
    [Fact]
    public async Task CustomerItem_WithNestedAddress_SerializesDeepMap()
    {
        var entity = new CustomerItem
        {
            Pk = "TEST#PA",
            Sk = "CUSTOMER#WRITE-3",
            Version = 1,
            Email = "deepmap@test.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 03, 05, 12, 00, 00, TimeSpan.Zero),
            Profile = new CustomerProfile
            {
                DisplayName = "Deep Test",
                Nickname = null,
                PreferredAddress = new Address
                {
                    Line1 = "42 Nested Rd",
                    City = "Mapville",
                    Country = "US",
                    PostalCode = "12345",
                },
                BillingAddress = null,
            },
        };

        Db.Customers.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);
        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'CreatedAt': ?, 'Email': ?, 'IsPreferred': ?, 'Notes': ?, 'NullableNote': ?, 'Preferences': ?, 'Tags': ?, 'Version': ?, 'Contacts': ?, 'Profile': ?}
            """);

        var item = await GetItemAsync("TEST#PA", "CUSTOMER#WRITE-3", CancellationToken);
        item.Should().NotBeNull();

        item!.Should().ContainKey("Profile");
        SaveChangesCustomerItemMapper.FromItem(item).Should().BeEquivalentTo(entity);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  OwnsMany  →  L of M
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An OwnsMany navigation with two elements should produce a DynamoDB list (L) where each
    /// element is a map (M) of the owned type's scalar properties.
    /// </summary>
    [Fact]
    public async Task CustomerItem_WithContacts_SerializesAsListOfMaps()
    {
        var entity = new CustomerItem
        {
            Pk = "TEST#C",
            Sk = "CUSTOMER#WRITE-4",
            Version = 1,
            Email = "contacts@test.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 03, 06, 12, 00, 00, TimeSpan.Zero),
            Contacts =
            [
                new CustomerContact
                {
                    Kind = "email",
                    Value = "a@b.com",
                    Verified = true,
                    VerifiedAt =
                        new DateTimeOffset(2026, 01, 01, 00, 00, 00, TimeSpan.Zero),
                    Address = null,
                },
                new CustomerContact
                {
                    Kind = "phone",
                    Value = "+10005551234",
                    Verified = false,
                    VerifiedAt = null,
                    Address = new Address
                    {
                        Line1 = "1 Phone St",
                        City = "Callton",
                        Country = "US",
                        PostalCode = null,
                    },
                },
            ],
        };

        Db.Customers.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);
        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'CreatedAt': ?, 'Email': ?, 'IsPreferred': ?, 'Notes': ?, 'NullableNote': ?, 'Preferences': ?, 'Tags': ?, 'Version': ?, 'Contacts': ?}
            """);

        var item = await GetItemAsync("TEST#C", "CUSTOMER#WRITE-4", CancellationToken);
        item.Should().NotBeNull();

        item!.Should().ContainKey("Contacts");
        var contacts = item["Contacts"].L;
        contacts.Should().HaveCount(2);

        var first = contacts[0].M;
        first["Kind"].S.Should().Be("email");
        first["Value"].S.Should().Be("a@b.com");
        first["Verified"].BOOL.Should().BeTrue();
        first["VerifiedAt"]
            .S
            .Should()
            .Be(
                GetExpectedProviderString<CustomerContact>(
                    nameof(CustomerContact.VerifiedAt),
                    entity.Contacts[0].VerifiedAt));
        first.Should().NotContainKey("Address");

        var second = contacts[1].M;
        second["Kind"].S.Should().Be("phone");
        second["Verified"].BOOL.Should().BeFalse();
        second["VerifiedAt"].NULL.Should().BeTrue();
        second.Should().ContainKey("Address");
        second["Address"].M["City"].S.Should().Be("Callton");
        second["Address"].M["PostalCode"].NULL.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  String set  →  SS
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A <c>HashSet&lt;string&gt;</c> property must be serialized as a DynamoDB string set (SS),
    /// not as a list (L).
    /// </summary>
    [Fact]
    public async Task CustomerItem_WithStringSet_SerializesAsSS()
    {
        var entity = new CustomerItem
        {
            Pk = "TEST#SS",
            Sk = "CUSTOMER#WRITE-5",
            Version = 1,
            Email = "sset@test.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 03, 07, 12, 00, 00, TimeSpan.Zero),
            Tags = ["alpha", "beta", "gamma"],
        };

        Db.Customers.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);
        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'CreatedAt': ?, 'Email': ?, 'IsPreferred': ?, 'Notes': ?, 'NullableNote': ?, 'Preferences': ?, 'Tags': ?, 'Version': ?, 'Contacts': ?}
            """);

        var item = await GetItemAsync("TEST#SS", "CUSTOMER#WRITE-5", CancellationToken);
        item.Should().NotBeNull();

        item!["Tags"].SS.Should().BeEquivalentTo(["alpha", "beta", "gamma"]);
        SaveChangesCustomerItemMapper.FromItem(item).Should().BeEquivalentTo(entity);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Numeric set  →  NS
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A <c>HashSet&lt;int&gt;</c> property must be serialized as a DynamoDB numeric set (NS)
    /// where each element is a string-encoded integer — the required DynamoDB wire format.
    /// </summary>
    [Fact]
    public async Task OrderItem_WithNumericSet_SerializesAsNS()
    {
        var entity = new OrderItem
        {
            Pk = "TEST#NS",
            Sk = "ORDER#WRITE-1",
            Version = 1,
            CustomerPk = "CUSTOMER#TEST",
            Status = "New",
            Total = 42.00m,
            RiskFlags = [1, 4, 9],
        };

        Db.Orders.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);
        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'AppliedCoupons': ?, 'CancellationReason': ?, 'ChargesByCode': ?, 'CustomerPk': ?, 'RiskFlags': ?, 'Status': ?, 'Total': ?, 'Version': ?, 'Lines': ?}
            """);

        var item = await GetItemAsync("TEST#NS", "ORDER#WRITE-1", CancellationToken);
        item.Should().NotBeNull();

        // DynamoDB stores numeric set values as strings
        item!["RiskFlags"].NS.Should().BeEquivalentTo(["1", "4", "9"]);
        SaveChangesOrderItemMapper.FromItem(item).Should().BeEquivalentTo(entity);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Dictionary<string, string>  →  M
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A <c>Dictionary&lt;string, string&gt;</c> property must be serialized as a DynamoDB map
    /// (M) with each value as an S attribute.
    /// </summary>
    [Fact]
    public async Task CustomerItem_WithStringDictionary_SerializesAsMap()
    {
        var entity = new CustomerItem
        {
            Pk = "TEST#D",
            Sk = "CUSTOMER#WRITE-6",
            Version = 1,
            Email = "dict@test.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 03, 08, 12, 00, 00, TimeSpan.Zero),
            Preferences = new Dictionary<string, string>
            {
                ["language"] = "en", ["theme"] = "dark",
            },
        };

        Db.Customers.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);
        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'CreatedAt': ?, 'Email': ?, 'IsPreferred': ?, 'Notes': ?, 'NullableNote': ?, 'Preferences': ?, 'Tags': ?, 'Version': ?, 'Contacts': ?}
            """);

        var item = await GetItemAsync("TEST#D", "CUSTOMER#WRITE-6", CancellationToken);
        item.Should().NotBeNull();

        item!["Preferences"].M.Should().ContainKeys("language", "theme");
        SaveChangesCustomerItemMapper.FromItem(item).Should().BeEquivalentTo(entity);
    }

    /// <summary>
    /// A <c>Dictionary&lt;string, decimal&gt;</c> property must be serialized as a DynamoDB map
    /// (M) with each value as an N attribute.
    /// </summary>
    [Fact]
    public async Task OrderItem_WithDecimalDictionary_SerializesAsMapOfN()
    {
        var entity = new OrderItem
        {
            Pk = "TEST#DN",
            Sk = "ORDER#WRITE-2",
            Version = 1,
            CustomerPk = "CUSTOMER#TEST",
            Status = "New",
            Total = 50.00m,
            ChargesByCode = new Dictionary<string, decimal>
            {
                ["shipping"] = 9.99m, ["tax"] = 4.50m,
            },
        };

        Db.Orders.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);
        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'AppliedCoupons': ?, 'CancellationReason': ?, 'ChargesByCode': ?, 'CustomerPk': ?, 'RiskFlags': ?, 'Status': ?, 'Total': ?, 'Version': ?, 'Lines': ?}
            """);

        var item = await GetItemAsync("TEST#DN", "ORDER#WRITE-2", CancellationToken);
        item.Should().NotBeNull();

        item!["ChargesByCode"].M.Should().ContainKeys("shipping", "tax");
        SaveChangesOrderItemMapper.FromItem(item).Should().BeEquivalentTo(entity);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Primitive list  →  L
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A <c>List&lt;string&gt;</c> property must be serialized as a DynamoDB list (L) where each
    /// element is an S attribute — not as a string set (SS).
    /// </summary>
    [Fact]
    public async Task CustomerItem_WithPrimitiveList_SerializesAsListOfS()
    {
        var entity = new CustomerItem
        {
            Pk = "TEST#L",
            Sk = "CUSTOMER#WRITE-L",
            Version = 1,
            Email = "list@test.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 03, 09, 12, 00, 00, TimeSpan.Zero),
            Notes = ["first note", "second note"],
        };

        Db.Customers.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);
        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'CreatedAt': ?, 'Email': ?, 'IsPreferred': ?, 'Notes': ?, 'NullableNote': ?, 'Preferences': ?, 'Tags': ?, 'Version': ?, 'Contacts': ?}
            """);

        var item = await GetItemAsync("TEST#L", "CUSTOMER#WRITE-L", CancellationToken);
        item.Should().NotBeNull();

        item!["Notes"].L.Should().HaveCount(2);
        SaveChangesCustomerItemMapper.FromItem(item).Should().BeEquivalentTo(entity);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Empty collections
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     An empty <c>HashSet&lt;string&gt;</c> (set property) must serialize as
    ///     <c>{ NULL = true }</c> because DynamoDB does not allow empty sets on the wire.
    /// </summary>
    [Fact]
    public async Task CustomerItem_WithEmptyStringSet_SerializesAsNull()
    {
        var entity = new CustomerItem
        {
            Pk = "TEST#ESS",
            Sk = "CUSTOMER#WRITE-ESS",
            Version = 1,
            Email = "emptyset@test.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 03, 10, 12, 00, 00, TimeSpan.Zero),
            Tags = [],
        };

        Db.Customers.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);

        var item = await GetItemAsync(entity.Pk, entity.Sk, CancellationToken);
        item.Should().NotBeNull();

        // Empty sets are not valid in DynamoDB — the provider must emit NULL.
        item!["Tags"].NULL.Should().BeTrue();
    }

    /// <summary>
    ///     An empty <c>List&lt;string&gt;</c> (list property) must serialize as <c>{ L = [] }</c>
    ///     because empty lists are valid in DynamoDB.
    /// </summary>
    [Fact]
    public async Task CustomerItem_WithEmptyList_SerializesAsEmptyL()
    {
        var entity = new CustomerItem
        {
            Pk = "TEST#EL",
            Sk = "CUSTOMER#WRITE-EL",
            Version = 1,
            Email = "emptylist@test.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 03, 11, 12, 00, 00, TimeSpan.Zero),
            Notes = [],
        };

        Db.Customers.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);

        var item = await GetItemAsync(entity.Pk, entity.Sk, CancellationToken);
        item.Should().NotBeNull();

        // Empty lists are valid in DynamoDB — the provider must emit L = [].
        item!["Notes"].L.Should().BeEmpty();
    }

    /// <summary>
    ///     An empty <c>Dictionary&lt;string, string&gt;</c> (map property) must serialize as
    ///     <c>{ M = {} }</c> because empty maps are valid in DynamoDB.
    /// </summary>
    [Fact]
    public async Task CustomerItem_WithEmptyDictionary_SerializesAsEmptyM()
    {
        var entity = new CustomerItem
        {
            Pk = "TEST#ED",
            Sk = "CUSTOMER#WRITE-ED",
            Version = 1,
            Email = "emptydict@test.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 03, 12, 12, 00, 00, TimeSpan.Zero),
            Preferences = [],
        };

        Db.Customers.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);

        var item = await GetItemAsync(entity.Pk, entity.Sk, CancellationToken);
        item.Should().NotBeNull();

        // Empty maps are valid in DynamoDB — the provider must emit M = {}.
        item!["Preferences"].M.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Full-shape baseline comparison
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a rich CustomerItem (all non-trivial fields populated) via SaveChanges, reads the
    /// raw DynamoDB item, and compares it attribute-by-attribute against the DynamoMapper-generated
    /// baseline to confirm the provider serializes every shape correctly.
    /// </summary>
    [Fact]
    public async Task CustomerItem_FullRichShape_MatchesDynamoMapperBaseline()
    {
        // Clone the first seed customer with a unique PK/SK so it does not collide with seed data.
        var source = SaveChangesTableItems.Customers[0];
        var entity = source with { Pk = "TEST#BL", Sk = "CUSTOMER#WRITE-8" };

        // Rebuild owned object graph (records are value-equal but not reference-equal to seed).
        // Explicitly re-create so the change tracker picks up fresh CLR references.
        entity = new CustomerItem
        {
            Pk = "TEST#BL",
            Sk = "CUSTOMER#WRITE-8",
            Version = source.Version,
            Email = source.Email,
            IsPreferred = source.IsPreferred,
            CreatedAt = source.CreatedAt,
            NullableNote = source.NullableNote,
            Preferences = source.Preferences,
            Tags = source.Tags,
            Notes = source.Notes,
            Profile =
                source.Profile is null
                    ? null
                    : new CustomerProfile
                    {
                        DisplayName = source.Profile.DisplayName,
                        Nickname = source.Profile.Nickname,
                        PreferredAddress =
                            source.Profile.PreferredAddress is null
                                ? null
                                : new Address
                                {
                                    Line1 = source.Profile.PreferredAddress.Line1,
                                    City = source.Profile.PreferredAddress.City,
                                    Country = source.Profile.PreferredAddress.Country,
                                    PostalCode =
                                        source.Profile.PreferredAddress.PostalCode,
                                },
                        BillingAddress =
                            source.Profile.BillingAddress is null
                                ? null
                                : new Address
                                {
                                    Line1 = source.Profile.BillingAddress.Line1,
                                    City = source.Profile.BillingAddress.City,
                                    Country = source.Profile.BillingAddress.Country,
                                    PostalCode =
                                        source.Profile.BillingAddress.PostalCode,
                                },
                    },
            Contacts =
                source
                    .Contacts
                    .Select(c => new CustomerContact
                    {
                        Kind = c.Kind,
                        Value = c.Value,
                        Verified = c.Verified,
                        VerifiedAt = c.VerifiedAt,
                        Address = c.Address is null
                            ? null
                            : new Address
                            {
                                Line1 = c.Address.Line1,
                                City = c.Address.City,
                                Country = c.Address.Country,
                                PostalCode = c.Address.PostalCode,
                            },
                    })
                    .ToList(),
        };

        Db.Customers.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);
        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'CreatedAt': ?, 'Email': ?, 'IsPreferred': ?, 'Notes': ?, 'NullableNote': ?, 'Preferences': ?, 'Tags': ?, 'Version': ?, 'Contacts': ?, 'Profile': ?}
            """);

        var actual = await GetItemAsync("TEST#BL", "CUSTOMER#WRITE-8", CancellationToken);
        actual.Should().NotBeNull();

        // Build the expected baseline from the DynamoMapper (authoritative serializer for tests).
        var expected = SaveChangesCustomerItemMapper.ToItem(entity);
        expected["$type"] = new AttributeValue { S = "CustomerItem" };
        expected["CreatedAt"] = new AttributeValue
        {
            S = GetExpectedProviderString<CustomerItem>(
                nameof(CustomerItem.CreatedAt),
                entity.CreatedAt),
        };

        if (expected.TryGetValue("Contacts", out var expectedContactsAttribute))
            for (var i = 0; i < entity.Contacts.Count; i++)
            {
                var verifiedAt = entity.Contacts[i].VerifiedAt;
                if (verifiedAt is null)
                    continue;

                expectedContactsAttribute.L[i].M["VerifiedAt"] = new AttributeValue
                {
                    S = GetExpectedProviderString<CustomerContact>(
                        nameof(CustomerContact.VerifiedAt),
                        verifiedAt),
                };
            }

        // Compare attribute-by-attribute for clear failure messages on mismatch.
        // DynamoMapper writes null owned-navigations as explicit NULL attributes; the provider
        // omits them (absent and NULL are semantically equivalent in DynamoDB). Only skip an
        // expected-NULL entry when that key is absent in actual — if actual has it (e.g. as a
        // null nullable string), the comparison proceeds normally.
        foreach (var (key, expectedValue) in expected)
        {
            if (expectedValue.NULL == true && !actual!.ContainsKey(key))
                continue; // DynamoMapper null vs. provider-omitted null — both valid

            actual!.Should().ContainKey(key, $"attribute '{key}' must be present");
            AssertAttributeValueEqual(expectedValue, actual![key], key);
        }

        // No unexpected extra attributes should be written (shadow FK keys etc.).
        // Expected count minus entries where DynamoMapper wrote NULL but provider omitted.
        var omittedNullCount =
            expected.Count(kv => kv.Value.NULL == true && !actual!.ContainsKey(kv.Key));
        actual!
            .Should()
            .HaveCount(
                expected.Count - omittedNullCount,
                "the item must contain exactly the mapped attributes — no shadow FK keys or ordinals");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Recursively compares two <see cref="AttributeValue"/> instances and fails with a clear
    /// path-qualified message if they differ.
    /// </summary>
    private static void AssertAttributeValueEqual(
        AttributeValue expected,
        AttributeValue actual,
        string path)
    {
        if (expected.NULL == true)
        {
            actual.NULL.Should().BeTrue($"attribute '{path}' should be NULL");
            return;
        }

        if (expected.S is not null)
        {
            actual.S.Should().Be(expected.S, $"attribute '{path}'.S mismatch");
            return;
        }

        if (expected.N is not null)
        {
            actual.N.Should().Be(expected.N, $"attribute '{path}'.N mismatch");
            return;
        }

        if (expected.BOOL == true)
        {
            actual.BOOL.Should().Be(expected.BOOL, $"attribute '{path}'.BOOL mismatch");
            return;
        }

        if (expected.SS is { Count: > 0 })
        {
            actual.SS.Should().BeEquivalentTo(expected.SS, $"attribute '{path}'.SS mismatch");
            return;
        }

        if (expected.NS is { Count: > 0 })
        {
            actual.NS.Should().BeEquivalentTo(expected.NS, $"attribute '{path}'.NS mismatch");
            return;
        }

        if (expected.M is { Count: > 0 })
        {
            actual.M.Should().NotBeNull($"attribute '{path}' should be a map");
            // Apply the same absent-NULL tolerance used at the top level: DynamoMapper writes
            // null owned-navigations as explicit NULL; the provider omits them (semantically
            // equal).
            foreach (var (key, expectedValue) in expected.M)
            {
                if (expectedValue.NULL == true && !actual.M.ContainsKey(key))
                    continue;

                actual.M.Should().ContainKey(key, $"map attribute '{path}.{key}' must be present");
                AssertAttributeValueEqual(expectedValue, actual.M[key], $"{path}.{key}");
            }

            var omittedNullCount = expected.M.Count(kv
                => kv.Value.NULL == true && !actual.M.ContainsKey(kv.Key));
            actual
                .M
                .Should()
                .HaveCount(
                    expected.M.Count - omittedNullCount,
                    $"map attribute '{path}' should have the same number of keys");
            return;
        }

        if (expected.L is { Count: > 0 })
        {
            actual
                .L
                .Should()
                .HaveCount(expected.L.Count, $"list attribute '{path}' length mismatch");
            for (var i = 0; i < expected.L.Count; i++)
                AssertAttributeValueEqual(expected.L[i], actual.L[i], $"{path}[{i}]");
            return;
        }

        // Empty M or L (both report Count 0 by default)
        if (expected.M is not null)
        {
            actual.M.Should().NotBeNull($"attribute '{path}' should be an empty map");
            actual.M.Should().BeEmpty($"attribute '{path}' should be an empty map");
            return;
        }

        if (expected.L is not null)
        {
            actual.L.Should().NotBeNull($"attribute '{path}' should be an empty list");
            actual.L.Should().BeEmpty($"attribute '{path}' should be an empty list");
        }
    }

    private string GetExpectedProviderString<TEntity>(string propertyName, object? value)
        where TEntity : class
        => (string?)GetExpectedProviderValue(typeof(TEntity), propertyName, value)
            ?? throw new InvalidOperationException(
                $"Expected provider value for '{typeof(TEntity).Name}.{propertyName}' to be a string.");

    private object? GetExpectedProviderValue(Type entityClrType, string propertyName, object? value)
    {
        if (value == null)
            return null;

        var property =
            Db
                .Model
                .GetEntityTypes()
                .Single(entityType
                    => entityType.ClrType == entityClrType
                    && entityType.FindProperty(propertyName) is not null)
                .FindProperty(propertyName)!;

        var converter = property.GetTypeMapping().Converter;
        return converter == null ? value : converter.ConvertToProvider(value);
    }
}
