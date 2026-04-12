using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>Integration tests for <c>SaveChangesAsync</c> with <c>EntityState.Added</c> entities (scalar properties only).</summary>
public class AddedEntitiesSaveChangesTests(SaveChangesTableDynamoFixture fixture)
    : SaveChangesTableTestBase(fixture)
{
    /// <summary>A newly added scalar-only ProductItem round-trips correctly to DynamoDB.</summary>
    [Fact]
    public async Task AddAsync_ProductItem_ScalarsOnly_Persists()
    {
        var product = new ProductItem
        {
            Pk = "PROD#round-trip",
            Sk = "PRODUCT",
            Version = 1,
            Name = "Widget",
            Price = 9.99m,
            IsActive = true,
            PublishedAt = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
            // Owned types intentionally null/empty — scalar-only for this story
        };

        Db.Products.Add(product);
        await Db.SaveChangesAsync(CancellationToken);

        var item = await GetItemAsync(product.Pk, product.Sk, CancellationToken);
        item.Should().NotBeNull();
        item!["Pk"].S.Should().Be("PROD#round-trip");
        item["Sk"].S.Should().Be("PRODUCT");
        item["Version"].N.Should().Be("1");
        item["Name"].S.Should().Be("Widget");
        item["Price"].N.Should().Be("9.99");
        item["IsActive"].BOOL.Should().BeTrue();
        item["PublishedAt"].S.Should().Be("2024-06-01 00:00:00+00:00");
        item["$type"].S.Should().Be("ProductItem");

        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'CategorySet': ?, 'IsActive': ?, 'Metadata': ?, 'Name': ?, 'Price': ?, 'PublishedAt': ?, 'SearchTerms': ?, 'Version': ?, 'Variants': ?}
            """);
    }

    /// <summary>The discriminator attribute is written correctly for each entity type in the shared table.</summary>
    [Fact]
    public async Task AddAsync_WritesDiscriminator_ForEachEntityType()
    {
        var customer = new CustomerItem
        {
            Pk = "CUST#disc-test",
            Sk = "PROFILE",
            Version = 1,
            Email = "test@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var order = new OrderItem
        {
            Pk = "CUST#disc-test",
            Sk = "ORDER#001",
            Version = 1,
            CustomerPk = "CUST#disc-test",
            Status = "Pending",
            Total = 50m,
        };
        var product = new ProductItem
        {
            Pk = "PROD#disc-test",
            Sk = "PRODUCT",
            Version = 1,
            Name = "Gadget",
            Price = 19.99m,
            IsActive = true,
        };
        var session = new SessionItem
        {
            Pk = "SESS#disc-test",
            Sk = "SESSION",
            Version = 1,
            CustomerPk = "CUST#disc-test",
            ExpiresAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Revoked = false,
        };

        Db.Customers.Add(customer);
        Db.Orders.Add(order);
        Db.Products.Add(product);
        Db.Sessions.Add(session);
        await Db.SaveChangesAsync(CancellationToken);

        var customerItem = await GetItemAsync(customer.Pk, customer.Sk, CancellationToken);
        var orderItem = await GetItemAsync(order.Pk, order.Sk, CancellationToken);
        var productItem = await GetItemAsync(product.Pk, product.Sk, CancellationToken);
        var sessionItem = await GetItemAsync(session.Pk, session.Sk, CancellationToken);

        customerItem!["$type"].S.Should().Be("CustomerItem");
        orderItem!["$type"].S.Should().Be("OrderItem");
        productItem!["$type"].S.Should().Be("ProductItem");
        sessionItem!["$type"].S.Should().Be("SessionItem");
    }

    /// <summary>A nullable scalar written as null is persisted as a DynamoDB NULL attribute.</summary>
    [Fact]
    public async Task AddAsync_NullableScalar_IsWrittenAsNull()
    {
        var product = new ProductItem
        {
            Pk = "PROD#nullable",
            Sk = "PRODUCT",
            Version = 1,
            Name = "Sparse Product",
            Price = 0m,
            IsActive = false,
            PublishedAt = null,
        };

        Db.Products.Add(product);
        await Db.SaveChangesAsync(CancellationToken);

        var item = await GetItemAsync(product.Pk, product.Sk, CancellationToken);
        item.Should().NotBeNull();
        item!["PublishedAt"].NULL.Should().BeTrue();
    }

    /// <summary>A newly added entity with primitive collections persists list, map, and set wire shapes.</summary>
    [Fact]
    public async Task AddAsync_SessionItem_PrimitiveCollections_Persist()
    {
        var session = new SessionItem
        {
            Pk = "SESS#collections",
            Sk = "SESSION",
            Version = 1,
            CustomerPk = "CUST#collections",
            ExpiresAt = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Scopes = ["read", "write"],
            Attributes =
                new Dictionary<string, string>
                {
                    ["device"] = "ios", ["region"] = "eu-west-1",
                },
            Flags = ["trusted", "mfa"],
            Revoked = false,
        };

        Db.Sessions.Add(session);
        await Db.SaveChangesAsync(CancellationToken);

        var item = await GetItemAsync(session.Pk, session.Sk, CancellationToken);
        item.Should().NotBeNull();
        item!["Scopes"].L.Select(x => x.S).Should().Equal("read", "write");
        item["Attributes"].M["device"].S.Should().Be("ios");
        item["Attributes"].M["region"].S.Should().Be("eu-west-1");
        item["Flags"].SS.Should().BeEquivalentTo("trusted", "mfa");

        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'Attributes': ?, 'CustomerPk': ?, 'ExpiresAt': ?, 'Flags': ?, 'LastSeenAt': ?, 'Revoked': ?, 'Scopes': ?, 'Version': ?}
            """);
    }

    /// <summary>Entity state transitions from Added to Unchanged after a successful SaveChanges.</summary>
    [Fact]
    public async Task AddAsync_EntityStateTransitionsToUnchanged()
    {
        var session = new SessionItem
        {
            Pk = "SESS#state-test",
            Sk = "SESSION",
            Version = 1,
            CustomerPk = "CUST#1",
            ExpiresAt = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Revoked = false,
        };

        Db.Entry(session).State.Should().Be(EntityState.Detached);

        Db.Sessions.Add(session);
        Db.Entry(session).State.Should().Be(EntityState.Added);

        await Db.SaveChangesAsync(CancellationToken);

        Db.Entry(session).State.Should().Be(EntityState.Unchanged);
    }

    /// <summary>
    /// Attempting to add an entity with a PK that already exists throws an exception,
    /// confirming INSERT (create-only) semantics rather than PutItem (replace) semantics.
    /// </summary>
    [Fact]
    public async Task AddAsync_DuplicateKey_ThrowsException()
    {
        // Seed the item directly so a duplicate is guaranteed
        await PutItemAsync(
            new Dictionary<string, AttributeValue>
            {
                ["Pk"] = new() { S = "PROD#dup-test" },
                ["Sk"] = new() { S = "PRODUCT" },
                ["$type"] = new() { S = "ProductItem" },
                ["Name"] = new() { S = "Existing" },
                ["Version"] = new() { N = "1" },
                ["Price"] = new() { N = "5" },
                ["IsActive"] = new() { BOOL = true },
            },
            CancellationToken);

        var duplicate = new ProductItem
        {
            Pk = "PROD#dup-test",
            Sk = "PRODUCT",
            Version = 2,
            Name = "Duplicate",
            Price = 10m,
            IsActive = false,
        };

        Db.Products.Add(duplicate);

        // DynamoDB INSERT into an existing key throws DuplicateItemException
        var act = async () => await Db.SaveChangesAsync(CancellationToken);
        await act.Should().ThrowAsync<Exception>();
    }

    /// <summary>SaveChangesAsync returns the count of added entities.</summary>
    [Fact]
    public async Task SaveChangesAsync_ReturnsCorrectCount_ForMultipleAdds()
    {
        var orders =
            Enumerable
                .Range(1, 3)
                .Select(i => new OrderItem
                {
                    Pk = "CUST#count-test",
                    Sk = $"ORDER#00{i}",
                    Version = 1,
                    CustomerPk = "CUST#count-test",
                    Status = "Pending",
                    Total = i * 10m,
                })
                .ToList();

        foreach (var order in orders)
            Db.Orders.Add(order);

        var count = await Db.SaveChangesAsync(CancellationToken);
        count.Should().Be(3);
    }
}
