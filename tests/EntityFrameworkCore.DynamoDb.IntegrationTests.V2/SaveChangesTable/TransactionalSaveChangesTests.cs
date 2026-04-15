using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SaveChangesTable;

/// <summary>Integration tests for multi-root SaveChanges transactional execution semantics.</summary>
public class TransactionalSaveChangesTests(DynamoContainerFixture fixture)
    : SaveChangesTableTestFixture(fixture)
{
    [Fact]
    public async Task WhenNeeded_MultiRootSave_SucceedsAtomically()
    {
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.WhenNeeded;

        var first = CreateCustomer("TENANT#TXN", "CUSTOMER#WHENNEEDED-OK-1", "first@example.com");
        var second = CreateCustomer("TENANT#TXN", "CUSTOMER#WHENNEEDED-OK-2", "second@example.com");

        Db.Customers.Add(first);
        Db.Customers.Add(second);

        await Db.SaveChangesAsync(CancellationToken);

        (await GetItemAsync(first.Pk, first.Sk, CancellationToken)).Should().NotBeNull();
        (await GetItemAsync(second.Pk, second.Sk, CancellationToken)).Should().NotBeNull();

        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'CreatedAt': ?, 'Email': ?, 'IsPreferred': ?, 'Notes': ?, 'NullableNote': ?, 'Preferences': ?, 'ReferenceIds': ?, 'Tags': ?, 'Version': ?, 'Contacts': ?}
            """,
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'CreatedAt': ?, 'Email': ?, 'IsPreferred': ?, 'Notes': ?, 'NullableNote': ?, 'Preferences': ?, 'ReferenceIds': ?, 'Tags': ?, 'Version': ?, 'Contacts': ?}
            """);
    }

    [Fact]
    public async Task WhenNeeded_MultiRootFailure_RollsBackAndKeepsEntriesPending()
    {
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.WhenNeeded;

        await PutItemAsync(
            CreateSeedItem(
                CreateCustomer("TENANT#TXN", "CUSTOMER#WHENNEEDED-DUP", "existing@example.com")),
            CancellationToken);

        var first = CreateCustomer(
            "TENANT#TXN",
            "CUSTOMER#WHENNEEDED-ROLLBACK-1",
            "first@example.com");
        var duplicate =
            CreateCustomer("TENANT#TXN", "CUSTOMER#WHENNEEDED-DUP", "duplicate@example.com");

        Db.Customers.Add(first);
        Db.Customers.Add(duplicate);

        var act = async () => await Db.SaveChangesAsync(CancellationToken);
        await act.Should().ThrowAsync<DbUpdateException>();

        (await GetItemAsync(first.Pk, first.Sk, CancellationToken)).Should().BeNull();
        Db.Entry(first).State.Should().Be(EntityState.Added);
        Db.Entry(duplicate).State.Should().Be(EntityState.Added);
    }

    [Fact]
    public async Task Never_MultiRootFailure_AllowsPartialCommit()
    {
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;

        await PutItemAsync(
            CreateSeedItem(
                CreateCustomer("TENANT#TXN", "CUSTOMER#NEVER-DUP", "existing@example.com")),
            CancellationToken);

        var first = CreateCustomer("TENANT#TXN", "CUSTOMER#NEVER-PARTIAL-1", "first@example.com");
        var duplicate = CreateCustomer("TENANT#TXN", "CUSTOMER#NEVER-DUP", "duplicate@example.com");

        Db.Customers.Add(first);
        Db.Customers.Add(duplicate);

        var act = async () => await Db.SaveChangesAsync(CancellationToken);
        await act.Should().ThrowAsync<DbUpdateException>();

        (await GetItemAsync(first.Pk, first.Sk, CancellationToken)).Should().NotBeNull();
    }

    [Fact]
    public async Task Never_MultiRootBatch_SaveChangesFalse_ThrowsClearError()
    {
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;

        var first = CreateCustomer("TENANT#TXN", "CUSTOMER#NEVER-FALSE-1", "first@example.com");
        var second = CreateCustomer("TENANT#TXN", "CUSTOMER#NEVER-FALSE-2", "second@example.com");

        Db.Customers.Add(first);
        Db.Customers.Add(second);

        var act = async () => await Db.SaveChangesAsync(false, CancellationToken);
        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*acceptAllChangesOnSuccess is false*");

        Db.Entry(first).State.Should().Be(EntityState.Added);
        Db.Entry(second).State.Should().Be(EntityState.Added);
    }

    [Fact]
    public async Task Never_BatchedChunkFailure_AcceptsSuccessfulPriorChunkEntries()
    {
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        Db.Database.SetMaxBatchWriteSize(2);

        await PutItemAsync(
            CreateSeedItem(
                CreateCustomer("TENANT#TXN", "CUSTOMER#NEVER-CHUNK-DUP", "existing@example.com")),
            CancellationToken);

        var first = CreateCustomer("TENANT#TXN", "CUSTOMER#NEVER-CHUNK-1", "first@example.com");
        var second = CreateCustomer("TENANT#TXN", "CUSTOMER#NEVER-CHUNK-2", "second@example.com");
        var duplicate = CreateCustomer(
            "TENANT#TXN",
            "CUSTOMER#NEVER-CHUNK-DUP",
            "duplicate@example.com");

        Db.Customers.Add(first);
        Db.Customers.Add(second);
        Db.Customers.Add(duplicate);

        var act = async () => await Db.SaveChangesAsync(CancellationToken);
        await act.Should().ThrowAsync<DbUpdateException>();

        Db.Entry(first).State.Should().Be(EntityState.Unchanged);
        Db.Entry(second).State.Should().Be(EntityState.Unchanged);
        Db.Entry(duplicate).State.Should().Be(EntityState.Added);

        (await GetItemAsync(first.Pk, first.Sk, CancellationToken)).Should().NotBeNull();
        (await GetItemAsync(second.Pk, second.Sk, CancellationToken)).Should().NotBeNull();
    }

    [Fact]
    public async Task Always_MoreThan100RootWrites_ThrowsClearErrorWithoutDowngrade()
    {
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;

        var customers = Enumerable
            .Range(0, 101)
            .Select(i => CreateCustomer(
                "TENANT#TXN",
                $"CUSTOMER#ALWAYS-LIMIT-{i:D3}",
                $"limit-{i}@example.com"))
            .ToList();

        Db.Customers.AddRange(customers);

        var act = async () => await Db.SaveChangesAsync(CancellationToken);
        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*MaxTransactionSize of 100*");

        (await GetItemAsync("TENANT#TXN", "CUSTOMER#ALWAYS-LIMIT-000", CancellationToken))
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task Always_MultiRootFailure_RollsBackAndKeepsEntriesPending()
    {
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;

        await PutItemAsync(
            CreateSeedItem(
                CreateCustomer("TENANT#TXN", "CUSTOMER#ALWAYS-DUP", "existing@example.com")),
            CancellationToken);

        var first = CreateCustomer("TENANT#TXN", "CUSTOMER#ALWAYS-ROLLBACK-1", "first@example.com");
        var duplicate =
            CreateCustomer("TENANT#TXN", "CUSTOMER#ALWAYS-DUP", "duplicate@example.com");

        Db.Customers.Add(first);
        Db.Customers.Add(duplicate);

        var act = async () => await Db.SaveChangesAsync(CancellationToken);
        await act.Should().ThrowAsync<DbUpdateException>();

        (await GetItemAsync(first.Pk, first.Sk, CancellationToken)).Should().BeNull();
        Db.Entry(first).State.Should().Be(EntityState.Added);
        Db.Entry(duplicate).State.Should().Be(EntityState.Added);
    }

    [Fact]
    public async Task WhenNeeded_CancelledSave_DoesNotPersistAnyItemAndKeepsEntriesPending()
    {
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.WhenNeeded;

        var first = CreateCustomer(
            "TENANT#TXN",
            "CUSTOMER#WHENNEEDED-CANCEL-1",
            "first@example.com");
        var second = CreateCustomer(
            "TENANT#TXN",
            "CUSTOMER#WHENNEEDED-CANCEL-2",
            "second@example.com");

        Db.Customers.Add(first);
        Db.Customers.Add(second);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await Db.SaveChangesAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        (await GetItemAsync(first.Pk, first.Sk, CancellationToken)).Should().BeNull();
        (await GetItemAsync(second.Pk, second.Sk, CancellationToken)).Should().BeNull();
        Db.Entry(first).State.Should().Be(EntityState.Added);
        Db.Entry(second).State.Should().Be(EntityState.Added);
    }

    [Fact]
    public async Task WhenNeeded_OverflowWithUseChunking_AcceptsSuccessfulChunkEntries()
    {
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.WhenNeeded;
        Db.Database.SetTransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking);
        Db.Database.SetMaxTransactionSize(2);

        await PutItemAsync(
            CreateSeedItem(
                CreateCustomer("TENANT#TXN", "CUSTOMER#CHUNK-DUP", "existing@example.com")),
            CancellationToken);

        var first = CreateCustomer("TENANT#TXN", "CUSTOMER#CHUNK-FIRST", "first@example.com");
        var second = CreateCustomer("TENANT#TXN", "CUSTOMER#CHUNK-SECOND", "second@example.com");
        var duplicate = CreateCustomer("TENANT#TXN", "CUSTOMER#CHUNK-DUP", "duplicate@example.com");

        Db.Customers.Add(first);
        Db.Customers.Add(second);
        Db.Customers.Add(duplicate);

        var act = async () => await Db.SaveChangesAsync(CancellationToken);
        await act.Should().ThrowAsync<DbUpdateException>();

        (await GetItemAsync(first.Pk, first.Sk, CancellationToken)).Should().NotBeNull();
        (await GetItemAsync(second.Pk, second.Sk, CancellationToken)).Should().NotBeNull();
        Db.Entry(first).State.Should().Be(EntityState.Unchanged);
        Db.Entry(second).State.Should().Be(EntityState.Unchanged);
        Db.Entry(duplicate).State.Should().Be(EntityState.Added);
    }

    [Fact]
    public async Task Always_OverflowWithUseChunking_ThrowsInsteadOfChunking()
    {
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
        Db.Database.SetTransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking);
        Db.Database.SetMaxTransactionSize(2);

        var first = CreateCustomer("TENANT#TXN", "CUSTOMER#ALWAYS-CHUNK-1", "first@example.com");
        var second = CreateCustomer("TENANT#TXN", "CUSTOMER#ALWAYS-CHUNK-2", "second@example.com");
        var third = CreateCustomer("TENANT#TXN", "CUSTOMER#ALWAYS-CHUNK-3", "third@example.com");

        Db.Customers.Add(first);
        Db.Customers.Add(second);
        Db.Customers.Add(third);

        var act = async () => await Db.SaveChangesAsync(CancellationToken);
        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AutoTransactionBehavior.Always*");

        (await GetItemAsync(first.Pk, first.Sk, CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task StartupConfiguredChunking_AcceptsSuccessfulChunkEntries()
    {
        await PutItemAsync(
            CreateSeedItem(
                CreateCustomer("TENANT#TXN", "CUSTOMER#STARTUP-CHUNK-DUP", "existing@example.com")),
            CancellationToken);

        await using var configuredDb = CreateConfiguredContext(
            TransactionOverflowBehavior.UseChunking,
            2);
        configuredDb.Database.AutoTransactionBehavior = AutoTransactionBehavior.WhenNeeded;

        var first = CreateCustomer("TENANT#TXN", "CUSTOMER#STARTUP-CHUNK-1", "first@example.com");
        var second = CreateCustomer("TENANT#TXN", "CUSTOMER#STARTUP-CHUNK-2", "second@example.com");
        var duplicate = CreateCustomer(
            "TENANT#TXN",
            "CUSTOMER#STARTUP-CHUNK-DUP",
            "duplicate@example.com");

        configuredDb.Customers.Add(first);
        configuredDb.Customers.Add(second);
        configuredDb.Customers.Add(duplicate);

        var act = async () => await configuredDb.SaveChangesAsync(CancellationToken);
        await act.Should().ThrowAsync<DbUpdateException>();

        (await GetItemAsync(first.Pk, first.Sk, CancellationToken)).Should().NotBeNull();
        (await GetItemAsync(second.Pk, second.Sk, CancellationToken)).Should().NotBeNull();
        configuredDb.Entry(first).State.Should().Be(EntityState.Unchanged);
        configuredDb.Entry(second).State.Should().Be(EntityState.Unchanged);
        configuredDb.Entry(duplicate).State.Should().Be(EntityState.Added);
    }

    [Fact]
    public async Task
        WhenNeeded_OverflowWithUseChunking_RetryOnSameContext_ReplaysOnlyPendingEntries()
    {
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.WhenNeeded;
        Db.Database.SetTransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking);
        Db.Database.SetMaxTransactionSize(2);

        await PutItemAsync(
            CreateSeedItem(
                CreateCustomer("TENANT#TXN", "CUSTOMER#CHUNK-RETRY-DUP", "existing@example.com")),
            CancellationToken);

        var first = CreateCustomer("TENANT#TXN", "CUSTOMER#CHUNK-RETRY-1", "first@example.com");
        var second = CreateCustomer("TENANT#TXN", "CUSTOMER#CHUNK-RETRY-2", "second@example.com");
        var duplicate = CreateCustomer(
            "TENANT#TXN",
            "CUSTOMER#CHUNK-RETRY-DUP",
            "duplicate@example.com");

        Db.Customers.Add(first);
        Db.Customers.Add(second);
        Db.Customers.Add(duplicate);

        var firstSave = async () => await Db.SaveChangesAsync(CancellationToken);
        await firstSave.Should().ThrowAsync<DbUpdateException>();

        Db.Entry(first).State.Should().Be(EntityState.Unchanged);
        Db.Entry(second).State.Should().Be(EntityState.Unchanged);
        Db.Entry(duplicate).State.Should().Be(EntityState.Added);

        duplicate.Sk = "CUSTOMER#CHUNK-RETRY-3";
        duplicate.Email = "third@example.com";

        await Db.SaveChangesAsync(CancellationToken);

        (await GetItemAsync(first.Pk, first.Sk, CancellationToken)).Should().NotBeNull();
        (await GetItemAsync(second.Pk, second.Sk, CancellationToken)).Should().NotBeNull();
        (await GetItemAsync(duplicate.Pk, duplicate.Sk, CancellationToken)).Should().NotBeNull();
    }

    [Fact]
    public async Task WhenNeeded_OverflowWithUseChunking_SaveChangesFalse_ThrowsClearError()
    {
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.WhenNeeded;
        Db.Database.SetTransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking);
        Db.Database.SetMaxTransactionSize(2);

        var first = CreateCustomer("TENANT#TXN", "CUSTOMER#CHUNK-FALSE-1", "first@example.com");
        var second = CreateCustomer("TENANT#TXN", "CUSTOMER#CHUNK-FALSE-2", "second@example.com");
        var third = CreateCustomer("TENANT#TXN", "CUSTOMER#CHUNK-FALSE-3", "third@example.com");

        Db.Customers.Add(first);
        Db.Customers.Add(second);
        Db.Customers.Add(third);

        var act = async () => await Db.SaveChangesAsync(false, CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*acceptAllChangesOnSuccess is false*");

        (await GetItemAsync(first.Pk, first.Sk, CancellationToken)).Should().BeNull();
        (await GetItemAsync(second.Pk, second.Sk, CancellationToken)).Should().BeNull();
        (await GetItemAsync(third.Pk, third.Sk, CancellationToken)).Should().BeNull();
    }

    [Fact]
    public void DatabaseFacade_TransactionOverflowSettings_CanBeOverriddenPerContext()
    {
        Db.Database.GetTransactionOverflowBehavior().Should().Be(TransactionOverflowBehavior.Throw);
        Db.Database.GetMaxTransactionSize().Should().Be(100);
        Db.Database.GetMaxBatchWriteSize().Should().Be(25);

        Db.Database.SetTransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking);
        Db.Database.SetMaxTransactionSize(25);
        Db.Database.SetMaxBatchWriteSize(10);

        Db
            .Database
            .GetTransactionOverflowBehavior()
            .Should()
            .Be(TransactionOverflowBehavior.UseChunking);
        Db.Database.GetMaxTransactionSize().Should().Be(25);
        Db.Database.GetMaxBatchWriteSize().Should().Be(10);
    }

    [Fact]
    public async Task Always_SingleRootSave_ExecutesDirectly_WithoutTransaction()
    {
        // AutoTransactionBehavior.Always still executes a single-root save directly
        // (no ExecuteTransaction overhead) — confirmed by the single SQL statement logged.
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;

        var customer = CreateCustomer("TENANT#TXN", "CUSTOMER#ALWAYS-SINGLE", "single@example.com");
        Db.Customers.Add(customer);

        await Db.SaveChangesAsync(CancellationToken);

        (await GetItemAsync(customer.Pk, customer.Sk, CancellationToken)).Should().NotBeNull();

        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'CreatedAt': ?, 'Email': ?, 'IsPreferred': ?, 'Notes': ?, 'NullableNote': ?, 'Preferences': ?, 'ReferenceIds': ?, 'Tags': ?, 'Version': ?, 'Contacts': ?}
            """);
    }

    [Fact]
    public async Task WhenNeeded_DuplicateTargetInSingleTransaction_ThrowsClearError()
    {
        // Two different entity types mapping to the same DynamoDB item key in one save —
        // ExecuteTransaction rejects multiple operations targeting the same item.
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.WhenNeeded;

        var customer = CreateCustomer("TENANT#TXN", "CUSTOMER#DUP-TARGET", "dup@example.com");

        var product = new ProductItem
        {
            // Same PK+SK as customer — both map to AppItems; same DynamoDB item.
            Pk = "TENANT#TXN",
            Sk = "CUSTOMER#DUP-TARGET",
            Version = 1,
            Name = "Collision",
            Price = 9.99m,
            IsActive = true,
        };

        Db.Customers.Add(customer);
        Db.Products.Add(product);

        var act = async () => await Db.SaveChangesAsync(CancellationToken);
        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*multiple operations targeting the same DynamoDB item*");

        (await GetItemAsync(customer.Pk, customer.Sk, CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task WhenNeeded_MixedStateTransaction_AllOperationsSucceedAtomically()
    {
        // Add + Modify + Delete in a single WhenNeeded save — all three states compile into
        // one ExecuteTransaction call that commits or rolls back together.
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.WhenNeeded;

        var toModify = CreateCustomer("TENANT#TXN", "CUSTOMER#MIXED-MODIFY", "modify@example.com");
        var toDelete = CreateCustomer("TENANT#TXN", "CUSTOMER#MIXED-DELETE", "delete@example.com");

        Db.Customers.Add(toModify);
        Db.Customers.Add(toDelete);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        var toAdd = CreateCustomer("TENANT#TXN", "CUSTOMER#MIXED-ADD", "add@example.com");
        Db.Customers.Add(toAdd);
        toModify.Email = "modified@example.com";
        Db.Customers.Remove(toDelete);

        await Db.SaveChangesAsync(CancellationToken);

        (await GetItemAsync(toAdd.Pk, toAdd.Sk, CancellationToken)).Should().NotBeNull();
        var modifiedItem = await GetItemAsync(toModify.Pk, toModify.Sk, CancellationToken);
        modifiedItem.Should().NotBeNull();
        modifiedItem!["Email"].S.Should().Be("modified@example.com");
        (await GetItemAsync(toDelete.Pk, toDelete.Sk, CancellationToken)).Should().BeNull();
    }

    /// <summary>
    ///     When a multi-root <c>SaveChanges</c> uses <c>TransactWriteItems</c> and one of the
    ///     operations has a stale concurrency token, DynamoDB returns <c>TransactionCanceledException</c>
    ///     with a <c>ConditionalCheckFailed</c> reason. The provider must map that to
    ///     <see cref="DbUpdateConcurrencyException" /> and roll back the entire transaction so the
    ///     un-conflicted write is also not persisted.
    /// </summary>
    [Fact]
    public async Task
        WhenNeeded_TransactionalStaleConcurrencyToken_ThrowsDbUpdateConcurrencyException()
    {
        Db.Database.AutoTransactionBehavior = AutoTransactionBehavior.WhenNeeded;

        // Insert two customers so both are tracked with Version = 1.
        var first = CreateCustomer("TENANT#TXN", "CUSTOMER#TXN-CONC-1", "first@example.com");
        var second = CreateCustomer("TENANT#TXN", "CUSTOMER#TXN-CONC-2", "second@example.com");

        Db.Customers.Add(first);
        Db.Customers.Add(second);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        // Simulate a concurrent writer bumping `second`'s version directly in DynamoDB.
        // The EF change tracker still holds Version = 1 for `second`.
        await BumpVersionAsync(second.Pk, second.Sk, CancellationToken);

        // Modify both — the WHERE clause for `second` will include "Version" = 1, which no
        // longer matches the stored value of 2.
        first.Email = "first-updated@example.com";
        second.Email = "second-updated@example.com";

        var act = async () => await Db.SaveChangesAsync(CancellationToken);
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

        // The transaction rolled back atomically: `first` must not have been persisted.
        var firstItem = await GetItemAsync(first.Pk, first.Sk, CancellationToken);
        firstItem!["Email"].S.Should().Be("first@example.com");

        // Both entries should remain Modified so the caller can retry after resolving the conflict.
        Db.Entry(first).State.Should().Be(EntityState.Modified);
        Db.Entry(second).State.Should().Be(EntityState.Modified);
    }

    private static CustomerItem CreateCustomer(string pk, string sk, string email)
        => new()
        {
            Pk = pk,
            Sk = sk,
            Version = 1,
            Email = email,
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private SaveChangesTableDbContext CreateConfiguredContext(
        TransactionOverflowBehavior behavior,
        int maxTransactionSize)
        => new(
            new DbContextOptionsBuilder<SaveChangesTableDbContext>()
                .UseDynamo(options
                    => options
                        .DynamoDbClient(Client)
                        .TransactionOverflowBehavior(behavior)
                        .MaxTransactionSize(maxTransactionSize))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

    private static Dictionary<string, AttributeValue> CreateSeedItem(CustomerItem customer)
    {
        var item = SaveChangesCustomerItemMapper.ToItem(customer);
        item["$type"] = new AttributeValue { S = nameof(CustomerItem) };
        return item;
    }
}
