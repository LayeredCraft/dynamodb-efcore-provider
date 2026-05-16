using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>Integration tests for <c>SaveChangesAsync</c> with <c>EntityState.Deleted</c> entities.</summary>
public class DeletedEntitiesSaveChangesTests(DynamoContainerFixture fixture)
    : SaveChangesTableTestFixture(fixture)
{
    /// <summary>
    ///     A tracked entity removed via <c>DbSet.Remove</c> is deleted from DynamoDB and the item is
    ///     absent on a subsequent raw SDK read.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task DeleteAsync_TrackedEntity_RemovesItemFromDynamoDB()
    {
        // Insert a fresh customer — avoid seeded entities whose Preferences map cannot yet be
        // materialized by the query pipeline (pre-existing limitation, unrelated to this story).
        var customer = new CustomerItem
        {
            Pk = "TENANT#DEL",
            Sk = "CUSTOMER#DEL-1",
            Version = 1,
            Email = "del@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        };
        await SeedAsync(customer);

        Db.Customers.Remove(customer);
        await SaveChangesShouldAffectAsync(1);

        await AssertItemDoesNotExistAsync(customer.Pk, customer.Sk, CancellationToken);

        AssertSql(
            """
            DELETE FROM "AppItems"
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    /// <summary>After a successful delete, EF transitions the entry state to Detached.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task DeleteAsync_EntityStateTransitionsToDetached()
    {
        var customer = new CustomerItem
        {
            Pk = "TENANT#DEL",
            Sk = "CUSTOMER#STATE-1",
            Version = 1,
            Email = "del-state@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        };
        await SeedAsync(customer);

        AssertEntryState(customer, EntityState.Unchanged);

        Db.Customers.Remove(customer);
        AssertEntryState(customer, EntityState.Deleted);

        await Db.SaveChangesAsync(CancellationToken);

        AssertEntryState(customer, EntityState.Detached);

        AssertSql(
            """
            DELETE FROM "AppItems"
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    /// <summary>
    ///     Deleting a stub entity whose key doesn't exist in DynamoDB is a no-op — the DynamoDB
    ///     PartiQL DELETE is idempotent. No exception is thrown and the entry transitions to Detached.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task DeleteAsync_NonExistentKey_IsNoOp()
    {
        var stub = new CustomerItem
        {
            Pk = "TENANT#GHOST",
            Sk = "CUSTOMER#GHOST-1",
            Version = 1,
            Email = "ghost@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        };

        // Attach and mark Deleted without inserting first — key does not exist in DynamoDB.
        Db.Customers.Attach(stub);
        Db.Entry(stub).State = EntityState.Deleted;

        await SaveChangesShouldAffectAsync(1);
        AssertEntryState(stub, EntityState.Detached);

        // Confirm item is still absent (was never created).
        await AssertItemDoesNotExistAsync("TENANT#GHOST", "CUSTOMER#GHOST-1", CancellationToken);
    }

    /// <summary>
    ///     A single <c>SaveChangesAsync</c> call containing Added, Modified, and Deleted entries
    ///     processes all three and returns the correct affected count with correct post-save tracking
    ///     states.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_MixedAddedModifiedDeleted_BatchAllStates()
    {
        // Pre-insert two entities to serve as modify and delete targets.
        var toModify = new CustomerItem
        {
            Pk = "TENANT#MIXED",
            Sk = "CUSTOMER#MIXED-MOD",
            Version = 1,
            Email = "before-modify@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var toDelete = new CustomerItem
        {
            Pk = "TENANT#MIXED",
            Sk = "CUSTOMER#MIXED-DEL",
            Version = 1,
            Email = "before-delete@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        };
        Db.Customers.AddRange(toModify, toDelete);
        await SaveAndClearLogAsync();

        // New entity to add in the mixed batch.
        var toAdd = new CustomerItem
        {
            Pk = "TENANT#MIXED",
            Sk = "CUSTOMER#MIXED-ADD",
            Version = 1,
            Email = "new@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        };

        Db.Customers.Add(toAdd);
        toModify.Email = "after-modify@example.com";
        Db.Customers.Remove(toDelete);

        await SaveChangesShouldAffectAsync(3);

        // Post-save state checks.
        AssertEntryStates(
            (toAdd, EntityState.Unchanged),
            (toModify, EntityState.Unchanged),
            (toDelete, EntityState.Detached));

        await AssertItemExistsInDynamoDbAsync(toAdd, toAdd.Pk, toAdd.Sk, CancellationToken);
        await AssertItemExistsInDynamoDbAsync(
            toModify,
            toModify.Pk,
            toModify.Sk,
            CancellationToken);

        await AssertItemDoesNotExistAsync(toDelete.Pk, toDelete.Sk, CancellationToken);

        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'createdAt': ?, 'email': ?, 'isPreferred': ?, 'notes': ?, 'nullableNote': ?, 'preferences': ?, 'referenceIds': ?, 'tags': ?, 'version': ?, 'contacts': ?}
            """,
            """
            UPDATE "AppItems"
            SET "email" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """,
            """
            DELETE FROM "AppItems"
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }
}
