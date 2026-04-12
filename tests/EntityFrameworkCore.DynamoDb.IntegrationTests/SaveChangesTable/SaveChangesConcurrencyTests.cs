using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>
///     Integration tests for explicit concurrency-token optimistic concurrency. Covers duplicate-key
///     INSERT behavior plus stale-token conflicts on UPDATE and DELETE.
/// </summary>
public class SaveChangesConcurrencyTests(SaveChangesTableDynamoFixture fixture)
    : SaveChangesTableTestBase(fixture)
{
    // ──────────────────────────────────────────────────────────────────────────────
    //  INSERT — duplicate key
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Inserting an entity whose PK+SK already exists in DynamoDB throws
    ///     <see cref="DbUpdateException" /> (inner: <c>DuplicateItemException</c>) — the provider maps the
    ///     AWS SDK exception to the appropriate EF exception type.
    /// </summary>
    [Fact]
    public async Task AddAsync_DuplicateKey_ThrowsDbUpdateException()
    {
        // Seed the item directly so the key already exists before the EF INSERT.
        await PutItemAsync(
            new Dictionary<string, AttributeValue>
            {
                ["Pk"] = new() { S = "TENANT#CONC" },
                ["Sk"] = new() { S = "CUSTOMER#DUP-1" },
                ["$type"] = new() { S = "CustomerItem" },
                ["Email"] = new() { S = "existing@example.com" },
                ["Version"] = new() { N = "1" },
                ["IsPreferred"] = new() { BOOL = false },
                ["CreatedAt"] = new() { S = "2026-04-01 00:00:00+00:00" },
            },
            CancellationToken);

        var duplicate = new CustomerItem
        {
            Pk = "TENANT#CONC",
            Sk = "CUSTOMER#DUP-1",
            Version = 1,
            Email = "duplicate@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        };
        Db.Customers.Add(duplicate);

        var act = async () => await Db.SaveChangesAsync(CancellationToken);
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  UPDATE — stale version
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     An out-of-band write that bumps <c>Version</c> while the application holds a stale entity
    ///     snapshot causes <see cref="DbUpdateConcurrencyException" /> on the next UPDATE.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_StaleVersion_ThrowsDbUpdateConcurrencyException()
    {
        var customer = new CustomerItem
        {
            Pk = "TENANT#CONC",
            Sk = "CUSTOMER#STALE-UPD",
            Version = 1,
            Email = "before@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        };
        Db.Customers.Add(customer);
        await Db.SaveChangesAsync(CancellationToken);

        // Simulate a concurrent writer bumping the version directly.
        await BumpVersionAsync(customer.Pk, customer.Sk, CancellationToken);

        // Modify and attempt to save — the WHERE "Version" = 1 predicate no longer matches.
        customer.Version = 2;
        customer.Email = "conflicted@example.com";
        var act = async () => await Db.SaveChangesAsync(CancellationToken);
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  DELETE — stale version
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     An out-of-band write that bumps <c>Version</c> while the application holds a stale entity
    ///     snapshot causes <see cref="DbUpdateConcurrencyException" /> on the next DELETE.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_StaleVersion_ThrowsDbUpdateConcurrencyException()
    {
        var customer = new CustomerItem
        {
            Pk = "TENANT#CONC",
            Sk = "CUSTOMER#STALE-DEL",
            Version = 1,
            Email = "to-delete@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        };
        Db.Customers.Add(customer);
        await Db.SaveChangesAsync(CancellationToken);

        // Simulate a concurrent writer bumping the version directly.
        await BumpVersionAsync(customer.Pk, customer.Sk, CancellationToken);

        // Attempt delete — the WHERE "Version" = 1 predicate no longer matches.
        Db.Customers.Remove(customer);
        var act = async () => await Db.SaveChangesAsync(CancellationToken);
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Manual token mutation verification
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>After a successful UPDATE, the manually assigned <c>Version</c> is persisted.</summary>
    [Fact]
    public async Task UpdateAsync_ManualTokenMutation_PersistsAssignedVersion()
    {
        var customer = new CustomerItem
        {
            Pk = "TENANT#CONC",
            Sk = "CUSTOMER#VER-INC",
            Version = 1,
            Email = "original@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        };
        Db.Customers.Add(customer);
        await Db.SaveChangesAsync(CancellationToken);

        customer.Version = 2;
        customer.Email = "updated@example.com";
        await Db.SaveChangesAsync(CancellationToken);

        var itemAfterUpdate = await GetItemAsync(customer.Pk, customer.Sk, CancellationToken);
        itemAfterUpdate!["Version"].N.Should().Be("2");
    }

    /// <summary>
    ///     Two sequential SaveChanges calls on the same entity both succeed when the application
    ///     advances the concurrency token from 1 → 2 → 3.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ConsecutiveSaves_TrackManualVersionCorrectly()
    {
        var customer = new CustomerItem
        {
            Pk = "TENANT#CONC",
            Sk = "CUSTOMER#VER-SEQ",
            Version = 1,
            Email = "v1@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        };
        Db.Customers.Add(customer);
        await Db.SaveChangesAsync(CancellationToken);

        // First update: Version 1 -> 2
        customer.Version = 2;
        customer.Email = "v2@example.com";
        await Db.SaveChangesAsync(CancellationToken);

        var itemV2 = await GetItemAsync(customer.Pk, customer.Sk, CancellationToken);
        itemV2!["Version"].N.Should().Be("2");

        // Second update: Version 2 -> 3
        customer.Version = 3;
        customer.Email = "v3@example.com";
        await Db.SaveChangesAsync(CancellationToken);

        var itemV3 = await GetItemAsync(customer.Pk, customer.Sk, CancellationToken);
        itemV3!["Version"].N.Should().Be("3");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Happy paths — ensure correct-version operations still succeed
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>An UPDATE with correct original token and new token value succeeds.</summary>
    [Fact]
    public async Task UpdateAsync_CorrectVersion_Succeeds()
    {
        var customer = new CustomerItem
        {
            Pk = "TENANT#CONC",
            Sk = "CUSTOMER#OK-UPD",
            Version = 1,
            Email = "before@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        };
        Db.Customers.Add(customer);
        await Db.SaveChangesAsync(CancellationToken);

        customer.Version = 2;
        customer.Email = "after@example.com";
        var act = async () => await Db.SaveChangesAsync(CancellationToken);
        await act.Should().NotThrowAsync();

        var item = await GetItemAsync(customer.Pk, customer.Sk, CancellationToken);
        item!["Email"].S.Should().Be("after@example.com");
    }

    /// <summary>A DELETE with the correct version succeeds and the item is gone.</summary>
    [Fact]
    public async Task DeleteAsync_CorrectVersion_Succeeds()
    {
        var customer = new CustomerItem
        {
            Pk = "TENANT#CONC",
            Sk = "CUSTOMER#OK-DEL",
            Version = 1,
            Email = "to-delete@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        };
        Db.Customers.Add(customer);
        await Db.SaveChangesAsync(CancellationToken);

        Db.Customers.Remove(customer);
        var act = async () => await Db.SaveChangesAsync(CancellationToken);
        await act.Should().NotThrowAsync();

        var item = await GetItemAsync(customer.Pk, customer.Sk, CancellationToken);
        item.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Directly increments the stored <c>Version</c> on an item to simulate a concurrent writer,
    ///     without going through EF Core.
    /// </summary>
    private async Task BumpVersionAsync(string pk, string sk, CancellationToken ct)
    {
        var item = await GetItemAsync(pk, sk, ct)
            ?? throw new InvalidOperationException(
                $"Cannot bump version: item {pk}/{sk} not found.");

        var currentVersion = long.Parse(item["Version"].N);
        item["Version"] = new AttributeValue { N = (currentVersion + 1).ToString() };
        await PutItemAsync(item, ct);
    }
}
