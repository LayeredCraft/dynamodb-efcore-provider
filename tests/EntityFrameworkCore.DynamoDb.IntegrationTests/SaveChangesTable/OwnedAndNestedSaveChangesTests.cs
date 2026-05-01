using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>
///     Integration tests for SaveChanges behaviour when complex properties, complex collections,
///     and primitive collections are mutated. Each test creates its own item, performs an initial
///     save, clears the SQL log, mutates, saves again, and then verifies the emitted PartiQL and
///     the persisted DynamoDB shape.
/// </summary>
public class OwnedAndNestedSaveChangesTests(DynamoContainerFixture fixture)
    : SaveChangesTableTestFixture(fixture)
{
    // -------------------------------------------------------------------------
    // ComplexProperty — scalar property changed
    // -------------------------------------------------------------------------

    /// <summary>
    ///     A modified scalar on a complex property reference emits a nested-path SET clause (
    ///     <c>SET "profile"."displayName" = ?</c>), not a full map replacement.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_OwnedReference_ScalarPropChanged_EmitsNestedPath()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#OWN-1",
            Version = 1,
            Email = "own1@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero),
            Profile = new CustomerProfile { DisplayName = "Before", Nickname = null },
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Profile!.DisplayName = "After";

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw.Should().NotBeNull();
        raw!["profile"].M["displayName"].S.Should().Be("After");

        // Nested-path shape — not a full map replace.
        AssertSql(
            """
            UPDATE "AppItems"
            SET "profile"."displayName" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // ComplexProperty — nullified (ref → null) emits REMOVE
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Setting a complex property reference to <see langword="null" /> emits
    ///     <c>REMOVE "profile"</c> which deletes the attribute from DynamoDB.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_OwnedReference_NullifiedToNull_EmitsRemove()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#OWN-2",
            Version = 1,
            Email = "own2@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 2, 12, 0, 0, TimeSpan.Zero),
            Profile = new CustomerProfile { DisplayName = "Will be removed", Nickname = null },
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Profile = null;

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw.Should().NotBeNull();

        // REMOVE deletes the attribute entirely — key must be absent.
        raw!.Should().NotContainKey("profile");

        AssertSql(
            """
            UPDATE "AppItems"
            REMOVE "profile"
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // ComplexProperty — set from null (null → ref) emits full map SET
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Assigning a complex property reference that was previously <see langword="null" /> emits
    ///     <c>SET "profile" = ?</c> with the full M value, because a nested SET path requires the
    ///     parent attribute to exist first.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_OwnedReference_SetFromNull_EmitsFullMapReplace()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#OWN-3",
            Version = 1,
            Email = "own3@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 3, 12, 0, 0, TimeSpan.Zero),
            Profile = null,
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Profile = new CustomerProfile { DisplayName = "Newly Added", Nickname = "new" };

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw.Should().NotBeNull();
        raw!.Should().ContainKey("profile");
        raw["profile"].M["displayName"].S.Should().Be("Newly Added");
        raw["profile"].M["nickname"].S.Should().Be("new");

        AssertSql(
            """
            UPDATE "AppItems"
            SET "profile" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // ComplexProperty — deeply nested path (profile.preferredAddress.city)
    // -------------------------------------------------------------------------

    /// <summary>
    ///     A scalar mutation two levels deep in a complex property chain emits a three-segment
    ///     nested-path SET clause rather than replacing the entire parent map.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_OwnedReference_DeeplyNested_EmitsThreeSegmentPath()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#OWN-4",
            Version = 1,
            Email = "own4@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 4, 12, 0, 0, TimeSpan.Zero),
            Profile = new CustomerProfile
            {
                DisplayName = "Depth Test",
                PreferredAddress = new Address
                {
                    Line1 = "1 Main St", City = "OldCity", Country = "US",
                },
            },
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Profile!.PreferredAddress!.City = "NewCity";

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw!["profile"].M["preferredAddress"].M["city"].S.Should().Be("NewCity");

        AssertSql(
            """
            UPDATE "AppItems"
            SET "profile"."preferredAddress"."city" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // ComplexCollection — element modified
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Modifying a property on a complex collection element replaces the entire list attribute
    ///     in one <c>SET "contacts" = ?</c> statement.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_OwnedCollection_ElementModified_ReplacesEntireList()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#OWN-5",
            Version = 1,
            Email = "own5@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero),
            Contacts =
            [
                new CustomerContact
                {
                    Kind = "email", Value = "own5@example.com", Verified = false,
                },
            ],
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Contacts[0].Verified = true;

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw!["contacts"].L.Should().HaveCount(1);
        raw["contacts"].L[0].M["verified"].BOOL.Should().BeTrue();

        AssertSql(
            """
            UPDATE "AppItems"
            SET "contacts" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // ComplexCollection — element added
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Adding a new element to a complex collection replaces the entire list attribute; the new
    ///     element appears at the end.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_OwnedCollection_ElementAdded_IncludesNewElementInList()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#OWN-6",
            Version = 1,
            Email = "own6@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 6, 12, 0, 0, TimeSpan.Zero),
            Contacts =
            [
                new CustomerContact
                {
                    Kind = "email", Value = "own6@example.com", Verified = true,
                },
            ],
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Contacts.Add(
            new CustomerContact { Kind = "phone", Value = "+15550001", Verified = false });

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw!["contacts"].L.Should().HaveCount(2);
        raw["contacts"].L[1].M["kind"].S.Should().Be("phone");
        raw["contacts"].L[1].M["verified"].BOOL.Should().BeFalse();

        AssertSql(
            """
            UPDATE "AppItems"
            SET "contacts" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // ComplexCollection — element removed
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Removing an element from a complex collection replaces the entire list attribute; the
    ///     removed element is absent from the persisted list.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_OwnedCollection_ElementRemoved_ExcludesRemovedElement()
    {
        var contact1 = new CustomerContact { Kind = "email", Value = "a@x.com", Verified = true };
        var contact2 = new CustomerContact { Kind = "phone", Value = "+10001", Verified = false };
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#OWN-7",
            Version = 1,
            Email = "own7@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 7, 12, 0, 0, TimeSpan.Zero),
            Contacts = [contact1, contact2],
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Contacts.Remove(contact1);

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw!["contacts"].L.Should().HaveCount(1);
        raw["contacts"].L[0].M["kind"].S.Should().Be("phone");

        AssertSql(
            """
            UPDATE "AppItems"
            SET "contacts" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // ComplexCollection — element with nested complex property modified
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Mutating a nested complex property inside a complex collection element still replaces
    ///     the entire parent list attribute — the nested change is captured in the rebuilt L value.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        SaveChangesAsync_OwnedCollection_WithNestedOwned_ReplacesListWithUpdatedNested()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#OWN-8",
            Version = 1,
            Email = "own8@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero),
            Contacts =
            [
                new CustomerContact
                {
                    Kind = "post",
                    Value = "mailing",
                    Verified = false,
                    Address = new Address
                    {
                        Line1 = "1 Old Rd", City = "OldCity", Country = "US",
                    },
                },
            ],
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Contacts[0].Address!.City = "NewCity";

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw!["contacts"].L.Should().HaveCount(1);
        raw["contacts"].L[0].M["address"].M["city"].S.Should().Be("NewCity");

        AssertSql(
            """
            UPDATE "AppItems"
            SET "contacts" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // ComplexCollection — nullified (collection → null) emits REMOVE
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Setting a complex collection to <see langword="null" /> emits <c>REMOVE "contacts"</c>
    ///     rather than writing an empty list.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_OwnedCollection_SetToNull_EmitsRemove()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#OWN-9",
            Version = 1,
            Email = "own9@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 9, 12, 0, 0, TimeSpan.Zero),
            Contacts =
            [
                new CustomerContact
                {
                    Kind = "email", Value = "own9@example.com", Verified = true,
                },
            ],
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Contacts = null!;

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw.Should().NotBeNull();
        raw!.Should().NotContainKey("contacts");

        AssertSql(
            """
            UPDATE "AppItems"
            REMOVE "contacts"
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // Primitive list (L) changed
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Replacing a primitive list property replaces the entire L attribute in one
    ///     <c>SET "appliedCoupons" = ?</c> statement.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_PrimitiveList_Changed_ReplacesListAttribute()
    {
        var item = new OrderItem
        {
            Pk = "TENANT#1",
            Sk = "ORDER#OWN-1",
            Version = 1,
            CustomerPk = "CUSTOMER#CUST-1",
            Status = "Placed",
            Total = 50m,
            AppliedCoupons = ["WELCOME10"],
        };
        Db.Orders.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        // Replace the collection reference so EF Core's snapshot comparison detects the change.
        item.AppliedCoupons = [..item.AppliedCoupons, "SUMMER5"];

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw!["appliedCoupons"].L.Select(a => a.S).Should().Contain("SUMMER5");
        raw["appliedCoupons"].L.Should().HaveCount(2);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "appliedCoupons" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // Primitive dictionary (M) changed
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Replacing a primitive dictionary property replaces the entire M attribute in one
    ///     <c>SET "chargesByCode" = ?</c> statement.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_PrimitiveDict_Changed_ReplacesMapAttribute()
    {
        var item = new OrderItem
        {
            Pk = "TENANT#1",
            Sk = "ORDER#OWN-2",
            Version = 1,
            CustomerPk = "CUSTOMER#CUST-1",
            Status = "Placed",
            Total = 60m,
            ChargesByCode = new Dictionary<string, decimal> { ["shipping"] = 9.99m },
        };
        Db.Orders.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        // Replace the dictionary reference so EF Core's snapshot comparison detects the change.
        item.ChargesByCode =
            new Dictionary<string, decimal>(item.ChargesByCode) { ["tax"] = 11.51m };

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw!["chargesByCode"].M.Should().ContainKey("tax");
        raw["chargesByCode"].M.Should().ContainKey("shipping");

        AssertSql(
            """
            UPDATE "AppItems"
            SET "chargesByCode" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // Primitive set (SS) changed — string elements
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Replacing a primitive string set property replaces the entire SS attribute in one
    ///     <c>SET "tags" = ?</c> statement.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_PrimitiveStringSet_Changed_ReplacesSetAttribute()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#OWN-11",
            Version = 1,
            Email = "own11@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 11, 12, 0, 0, TimeSpan.Zero),
            Tags = ["vip"],
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        // Replace the set reference so EF Core's snapshot comparison detects the change.
        item.Tags = new HashSet<string>(item.Tags) { "beta" };

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw!["tags"].SS.Should().Contain("beta");
        raw["tags"].SS.Should().Contain("vip");

        AssertSql(
            """
            UPDATE "AppItems"
            SET "tags" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // Primitive set (SS) changed — Guid elements (value-converter path)
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Replacing a <see cref="HashSet{Guid}" /> property replaces the SS attribute; GUIDs are
    ///     stored as their canonical string representation via the registered value converter.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_PrimitiveGuidSet_Changed_ReplacesSetAttribute()
    {
        var guid1 = Guid.Parse("11111111-0000-0000-0000-000000000001");
        var guid2 = Guid.Parse("11111111-0000-0000-0000-000000000002");
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#OWN-12",
            Version = 1,
            Email = "own12@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 12, 12, 0, 0, TimeSpan.Zero),
            ReferenceIds = [guid1],
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        // Replace the set reference so EF Core's snapshot comparison detects the change.
        item.ReferenceIds = new HashSet<Guid>(item.ReferenceIds) { guid2 };

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw!["referenceIds"].SS.Should().Contain(guid2.ToString());
        raw["referenceIds"].SS.Should().Contain(guid1.ToString());

        AssertSql(
            """
            UPDATE "AppItems"
            SET "referenceIds" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // Mixed: scalar + complex property mutation
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Simultaneously modifying a scalar root property and a nested complex property emits a
    ///     single UPDATE with both the scalar SET and the nested-path SET clause.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_Mixed_ScalarAndOwned_EmitsBothInSingleUpdate()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#OWN-13",
            Version = 1,
            Email = "own13-before@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 13, 12, 0, 0, TimeSpan.Zero),
            Profile = new CustomerProfile { DisplayName = "Test", Nickname = "before-nick" },
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Email = "own13-after@example.com";
        item.Profile!.Nickname = "after-nick";

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw!["email"].S.Should().Be("own13-after@example.com");
        raw["profile"].M["nickname"].S.Should().Be("after-nick");

        AssertSql(
            """
            UPDATE "AppItems"
            SET "email" = ?, "profile"."nickname" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // Mixed: scalar + primitive collection mutation
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Simultaneously modifying a scalar root property and a primitive set property emits a
    ///     single UPDATE with both SET clauses.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_Mixed_ScalarAndPrimitiveCollection_EmitsBothInSingleUpdate()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#OWN-14",
            Version = 1,
            Email = "own14-before@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 14, 12, 0, 0, TimeSpan.Zero),
            Tags = ["original"],
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Email = "own14-after@example.com";
        item.Tags = new HashSet<string>(item.Tags) { "added" };

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw!["email"].S.Should().Be("own14-after@example.com");
        raw["tags"].SS.Should().Contain("added");

        AssertSql(
            """
            UPDATE "AppItems"
            SET "email" = ?, "tags" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // Complex-property-only mutation — root stays Unchanged
    // -------------------------------------------------------------------------

    /// <summary>
    ///     When only a complex property changes and the root entity stays Unchanged, the provider
    ///     still emits an UPDATE for the root document — the aggregate root is included in the write
    ///     loop via <c>IncludeMutatingOwnedRoots</c>.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_OwnedOnly_NoRootScalars_EmitsUpdateForUnchangedRoot()
    {
        var item = new ProductItem
        {
            Pk = "TENANT#1",
            Sk = "PRODUCT#OWN-1",
            Version = 1,
            Name = "Test Product",
            Price = 10m,
            IsActive = true,
            Dimensions = new ProductDimensions { Height = 5m, Width = 3m, Depth = 1m },
        };
        Db.Products.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Dimensions!.Height = 15m;

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        // Decimal stored as DynamoDB N string.
        decimal.Parse(raw!["dimensions"].M["height"].N).Should().Be(15m);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "dimensions"."height" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // Concurrency token present in WHERE for owned mutation
    // -------------------------------------------------------------------------

    /// <summary>
    ///     An UPDATE for an owned navigation mutation includes the concurrency token property (
    ///     <c>"version" = ?</c>) in the WHERE clause, ensuring optimistic concurrency is enforced.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_ConcurrencyToken_OwnedMutation_IncludesVersionInWhere()
    {
        var item = new OrderItem
        {
            Pk = "TENANT#1",
            Sk = "ORDER#OWN-3",
            Version = 1,
            CustomerPk = "CUSTOMER#CUST-1",
            Status = "Placed",
            Total = 75m,
            Shipping = new ShippingDetails
            {
                Method = "Ground",
                Address = new Address
                {
                    Line1 = "1 Ship St", City = "Portland", Country = "US",
                },
            },
        };
        Db.Orders.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Shipping!.Method = "Express";

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw!["shipping"].M["method"].S.Should().Be("Express");

        // WHERE must include "version" = ? for concurrency enforcement.
        AssertSql(
            """
            UPDATE "AppItems"
            SET "shipping"."method" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    // -------------------------------------------------------------------------
    // Change tracker state after owned save
    // -------------------------------------------------------------------------

    /// <summary>
    ///     After a successful SaveChanges for an owned-property mutation, the owned entry transitions
    ///     back to <see cref="EntityState.Unchanged" /> and its original values are refreshed to match the
    ///     new current values, preventing a spurious second write.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_ChangeTracker_OwnedEntryIsUnchangedAfterSave()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#OWN-17",
            Version = 1,
            Email = "own17@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 17, 12, 0, 0, TimeSpan.Zero),
            Profile = new CustomerProfile { DisplayName = "Before", Nickname = null },
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Profile!.DisplayName = "After";
        var profileEntry = Db.Entry(item).ComplexProperty("Profile");
        profileEntry.IsModified.Should().BeTrue();

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        // Consume the first write's logged statement before checking the no-op save.
        AssertSql(
            """
            UPDATE "AppItems"
            SET "profile"."displayName" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);

        // Complex property entry must be reset and original values refreshed after SaveChanges.
        profileEntry.IsModified.Should().BeFalse();
        profileEntry.Property("DisplayName").OriginalValue.Should().Be("After");

        // A second SaveChanges must emit nothing — no spurious re-update.
        var secondAffected = await Db.SaveChangesAsync(CancellationToken);
        secondAffected.Should().Be(0);
        AssertSql();
    }
}
