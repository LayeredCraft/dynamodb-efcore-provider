using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>
///     Integration tests that verify the DynamoDB AttributeValue shapes written by
///     <c>SaveChangesAsync</c> for the full range of property shapes in the SaveChangesTable model:
///     scalars, owned references (OwnsOne → M), owned collections (OwnsMany → L of M), primitive lists
///     (L), string sets (SS), converter-backed Guid sets (SS), numeric sets (NS), and string-keyed
///     maps (M). Each test inserts via EF Core and then reads the raw item via the DynamoDB SDK to
///     assert the wire format rather than re-reading through the provider projection stack.
/// </summary>
public class WriteValueSerializationTests(DynamoContainerFixture fixture)
    : SaveChangesTableTestFixture(fixture)
{
    // ──────────────────────────────────────────────────────────────────────────────
    //  Scalar properties
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A minimal CustomerItem with only scalar properties (no owned objects, no collections)
    ///     should produce correct S, N, BOOL, and NULL AttributeValue wire representations.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'createdAt': ?, 'email': ?, 'isPreferred': ?, 'notes': ?, 'nullableNote': ?, 'preferences': ?, 'referenceIds': ?, 'tags': ?, 'version': ?, 'contacts': ?}
            """);

        await AssertItemExistsInDynamoDbAsync(entity, entity.Pk, entity.Sk, CancellationToken);
    }

    /// <summary>After SaveChanges the EF Core change tracker should reflect the entity as Unchanged.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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

        AssertEntryState(entity, EntityState.Added);

        await Db.SaveChangesAsync(CancellationToken);

        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'createdAt': ?, 'email': ?, 'isPreferred': ?, 'notes': ?, 'nullableNote': ?, 'preferences': ?, 'referenceIds': ?, 'tags': ?, 'version': ?, 'contacts': ?}
            """);

        AssertEntryState(entity, EntityState.Unchanged);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  OwnsOne  →  M
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A non-null OwnsOne navigation should produce a nested map (M) attribute whose keys match
    ///     the owned type's property names.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'createdAt': ?, 'email': ?, 'isPreferred': ?, 'notes': ?, 'nullableNote': ?, 'preferences': ?, 'referenceIds': ?, 'tags': ?, 'version': ?, 'contacts': ?, 'profile': ?}
            """);

        await AssertRawAttributeAsync(
            entity.Pk,
            entity.Sk,
            "profile",
            attribute =>
            {
                attribute.M.Should().ContainKey("displayName");
                attribute.M["displayName"].S.Should().Be("Test User");
                attribute.M["nickname"].S.Should().Be("tester");
            },
            CancellationToken);

        await AssertItemExistsInDynamoDbAsync(entity, entity.Pk, entity.Sk, CancellationToken);
    }

    /// <summary>A null OwnsOne navigation must not produce any attribute key in the item at all.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'createdAt': ?, 'email': ?, 'isPreferred': ?, 'notes': ?, 'nullableNote': ?, 'preferences': ?, 'referenceIds': ?, 'tags': ?, 'version': ?, 'contacts': ?}
            """);

        await AssertItemExistsInDynamoDbAsync(entity, entity.Pk, entity.Sk, CancellationToken);
    }

    /// <summary>
    ///     A deeply-nested OwnsOne chain (Profile → PreferredAddress → Address scalars) should
    ///     produce nested maps at each level without losing any scalar values.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'createdAt': ?, 'email': ?, 'isPreferred': ?, 'notes': ?, 'nullableNote': ?, 'preferences': ?, 'referenceIds': ?, 'tags': ?, 'version': ?, 'contacts': ?, 'profile': ?}
            """);

        await AssertRawAttributeAsync(
            entity.Pk,
            entity.Sk,
            "profile",
            attribute =>
            {
                attribute.M["displayName"].S.Should().Be("Deep Test");
                var address = attribute.M["preferredAddress"].M;
                address["line1"].S.Should().Be("42 Nested Rd");
                address["city"].S.Should().Be("Mapville");
            },
            CancellationToken);

        await AssertItemExistsInDynamoDbAsync(entity, entity.Pk, entity.Sk, CancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  OwnsMany  →  L of M
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     An OwnsMany navigation with two elements should produce a DynamoDB list (L) where each
    ///     element is a map (M) of the owned type's scalar properties.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'createdAt': ?, 'email': ?, 'isPreferred': ?, 'notes': ?, 'nullableNote': ?, 'preferences': ?, 'referenceIds': ?, 'tags': ?, 'version': ?, 'contacts': ?}
            """);

        await AssertRawAttributeAsync(
            entity.Pk,
            entity.Sk,
            "contacts",
            attribute =>
            {
                attribute.L.Should().HaveCount(2);
                attribute.L[0].M["kind"].S.Should().Be("email");
                attribute.L[0].M["verified"].BOOL.Should().BeTrue();
                attribute.L[1].M["address"].M["city"].S.Should().Be("Callton");
            },
            CancellationToken);

        await AssertItemExistsInDynamoDbAsync(entity, entity.Pk, entity.Sk, CancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  String set  →  SS
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A <c>HashSet&lt;string&gt;</c> property must be serialized as a DynamoDB string set (SS),
    ///     not as a list (L).
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'createdAt': ?, 'email': ?, 'isPreferred': ?, 'notes': ?, 'nullableNote': ?, 'preferences': ?, 'referenceIds': ?, 'tags': ?, 'version': ?, 'contacts': ?}
            """);

        await AssertRawStringSetAsync(
            entity.Pk,
            entity.Sk,
            "tags",
            ["alpha", "beta", "gamma"],
            CancellationToken);

        await AssertItemExistsInDynamoDbAsync(entity, entity.Pk, entity.Sk, CancellationToken);
    }

    /// <summary>
    ///     A <c>HashSet&lt;Guid&gt;</c> property must be serialized as a DynamoDB string set (SS) by
    ///     applying the element converter for each Guid.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task CustomerItem_WithGuidSet_SerializesAsSS()
    {
        var entity = new CustomerItem
        {
            Pk = "TEST#GSS",
            Sk = "CUSTOMER#WRITE-GSS",
            Version = 1,
            Email = "guidset@test.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 03, 07, 12, 30, 00, TimeSpan.Zero),
            ReferenceIds =
            [
                Guid.Parse("12345678-1234-1234-1234-1234567890ab"),
                Guid.Parse("abcdefab-cdef-cdef-cdef-abcdefabcdef"),
            ],
        };

        Db.Customers.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);
        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'createdAt': ?, 'email': ?, 'isPreferred': ?, 'notes': ?, 'nullableNote': ?, 'preferences': ?, 'referenceIds': ?, 'tags': ?, 'version': ?, 'contacts': ?}
            """);

        await AssertRawStringSetAsync(
            entity.Pk,
            entity.Sk,
            "referenceIds",
            entity.ReferenceIds!.Select(static x => x.ToString()),
            CancellationToken);

        var actual = (await GetExistingItemAsync(entity.Pk, entity.Sk, CancellationToken))
            .ToCustomerItem();
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(entity, options => options.Excluding(x => x.ReferenceIds));
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Numeric set  →  NS
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A <c>HashSet&lt;int&gt;</c> property must be serialized as a DynamoDB numeric set (NS)
    ///     where each element is a string-encoded integer — the required DynamoDB wire format.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'appliedCoupons': ?, 'cancellationReason': ?, 'chargesByCode': ?, 'customerPk': ?, 'riskFlags': ?, 'status': ?, 'total': ?, 'version': ?, 'lines': ?}
            """);

        await AssertRawAttributeAsync(
            entity.Pk,
            entity.Sk,
            "riskFlags",
            attribute => attribute.NS.Should().BeEquivalentTo("1", "4", "9"),
            CancellationToken);

        await AssertItemExistsInDynamoDbAsync(entity, entity.Pk, entity.Sk, CancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Dictionary<string, string>  →  M
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A <c>Dictionary&lt;string, string&gt;</c> property must be serialized as a DynamoDB map
    ///     (M) with each value as an S attribute.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'createdAt': ?, 'email': ?, 'isPreferred': ?, 'notes': ?, 'nullableNote': ?, 'preferences': ?, 'referenceIds': ?, 'tags': ?, 'version': ?, 'contacts': ?}
            """);

        await AssertRawAttributeAsync(
            entity.Pk,
            entity.Sk,
            "preferences",
            attribute =>
            {
                attribute.M["language"].S.Should().Be("en");
                attribute.M["theme"].S.Should().Be("dark");
            },
            CancellationToken);

        await AssertItemExistsInDynamoDbAsync(entity, entity.Pk, entity.Sk, CancellationToken);
    }

    /// <summary>
    ///     A <c>Dictionary&lt;string, decimal&gt;</c> property must be serialized as a DynamoDB map
    ///     (M) with each value as an N attribute.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'appliedCoupons': ?, 'cancellationReason': ?, 'chargesByCode': ?, 'customerPk': ?, 'riskFlags': ?, 'status': ?, 'total': ?, 'version': ?, 'lines': ?}
            """);

        await AssertRawAttributeAsync(
            entity.Pk,
            entity.Sk,
            "chargesByCode",
            attribute =>
            {
                attribute.M["shipping"].N.Should().Be("9.99");
                attribute.M["tax"].N.Should().Be("4.50");
            },
            CancellationToken);

        await AssertItemExistsInDynamoDbAsync(entity, entity.Pk, entity.Sk, CancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Primitive list  →  L
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A <c>List&lt;string&gt;</c> property must be serialized as a DynamoDB list (L) where each
    ///     element is an S attribute — not as a string set (SS).
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'createdAt': ?, 'email': ?, 'isPreferred': ?, 'notes': ?, 'nullableNote': ?, 'preferences': ?, 'referenceIds': ?, 'tags': ?, 'version': ?, 'contacts': ?}
            """);

        await AssertRawAttributeAsync(
            entity.Pk,
            entity.Sk,
            "notes",
            attribute => attribute.L.Select(x => x.S).Should().Equal("first note", "second note"),
            CancellationToken);

        await AssertItemExistsInDynamoDbAsync(entity, entity.Pk, entity.Sk, CancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Empty collections
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     An empty <c>HashSet&lt;string&gt;</c> (set property) must fail because DynamoDB does not allow
    ///     empty sets on the wire and silently writing <c>NULL</c> would lose type information.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task CustomerItem_WithEmptyStringSet_Throws()
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
        var act = () => Db.SaveChangesAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(
                "DynamoDB sets cannot be empty; use a null property or a non-empty collection.");
    }

    /// <summary>
    ///     An empty <c>List&lt;string&gt;</c> (list property) must serialize as <c>{ L = [] }</c>
    ///     because empty lists are valid in DynamoDB.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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

        await AssertRawAttributeAsync(
            entity.Pk,
            entity.Sk,
            "notes",
            attribute => attribute.L.Should().BeEmpty(),
            CancellationToken);

        await AssertItemExistsInDynamoDbAsync(entity, entity.Pk, entity.Sk, CancellationToken);
    }

    /// <summary>
    ///     An empty <c>Dictionary&lt;string, string&gt;</c> (map property) must serialize as
    ///     <c>{ M = {} }</c> because empty maps are valid in DynamoDB.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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

        await AssertRawAttributeAsync(
            entity.Pk,
            entity.Sk,
            "preferences",
            attribute => attribute.M.Should().BeEmpty(),
            CancellationToken);

        await AssertItemExistsInDynamoDbAsync(entity, entity.Pk, entity.Sk, CancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Full-shape baseline comparison
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Inserts a rich CustomerItem (all non-trivial fields populated) via SaveChanges, reads the
    ///     raw DynamoDB item, and compares it attribute-by-attribute against the DynamoMapper-generated
    ///     baseline to confirm the provider serializes every shape correctly.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            ReferenceIds = source.ReferenceIds,
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
                source.Contacts is null
                    ? null
                    : source
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
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'createdAt': ?, 'email': ?, 'isPreferred': ?, 'notes': ?, 'nullableNote': ?, 'preferences': ?, 'referenceIds': ?, 'tags': ?, 'version': ?, 'contacts': ?, 'profile': ?}
            """);

        await AssertRawStringSetAsync(
            entity.Pk,
            entity.Sk,
            "referenceIds",
            entity.ReferenceIds!.Select(static x => x.ToString()),
            CancellationToken);

        var actual = (await GetExistingItemAsync(entity.Pk, entity.Sk, CancellationToken))
            .ToCustomerItem();
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(entity, options => options.Excluding(x => x.ReferenceIds));
    }
}
