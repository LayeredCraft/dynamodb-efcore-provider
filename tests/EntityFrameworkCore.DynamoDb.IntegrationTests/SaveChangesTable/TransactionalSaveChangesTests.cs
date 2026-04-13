using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>Integration tests for multi-root SaveChanges transactional execution semantics.</summary>
public class TransactionalSaveChangesTests(SaveChangesTableDynamoFixture fixture)
    : SaveChangesTableTestBase(fixture)
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
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*limit of 100*");

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

    private static Dictionary<string, AttributeValue> CreateSeedItem(CustomerItem customer)
    {
        var item = SaveChangesCustomerItemMapper.ToItem(customer);
        item["$type"] = new AttributeValue { S = nameof(CustomerItem) };
        return item;
    }
}
