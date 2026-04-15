using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SaveChangesTable;

public class ModifiedScalarSaveChangesTests(DynamoContainerFixture fixture)
    : SaveChangesTableTestFixture(fixture)
{
    [Fact]
    public async Task SaveChangesAsync_ModifiedSingleScalar_PersistsAndEmitsUpdate()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#MOD-1",
            Version = 1,
            Email = "initial+1@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
            NullableNote = "note-1",
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Email = "updated+1@example.com";

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var actual = (await GetItemAsync(item.Pk, item.Sk, CancellationToken))?.ToCustomerItem();
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(item);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "Email" = ?
            WHERE "Pk" = ? AND "Sk" = ? AND "Version" = ?
            """);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedMultipleScalars_PersistsInSingleStatement()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#MOD-2",
            Version = 1,
            Email = "initial+2@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero),
            NullableNote = null,
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Email = "updated+2@example.com";
        item.IsPreferred = true;

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var actual = (await GetItemAsync(item.Pk, item.Sk, CancellationToken))?.ToCustomerItem();
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(item);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "Email" = ?, "IsPreferred" = ?
            WHERE "Pk" = ? AND "Sk" = ? AND "Version" = ?
            """);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedNullableScalar_NullTransitionPersists()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#MOD-3",
            Version = 1,
            Email = "initial+3@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 3, 3, 12, 0, 0, TimeSpan.Zero),
            NullableNote = "to-null",
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.NullableNote = null;

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var actual = (await GetItemAsync(item.Pk, item.Sk, CancellationToken))?.ToCustomerItem();
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(item);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "NullableNote" = ?
            WHERE "Pk" = ? AND "Sk" = ? AND "Version" = ?
            """);
    }

    [Fact]
    public async Task SaveChangesAsync_NoTrackedChanges_DoesNotEmitWrites()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#MOD-4",
            Version = 1,
            Email = "initial+4@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero),
            NullableNote = null,
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(0);

        var actual = (await GetItemAsync(item.Pk, item.Sk, CancellationToken))?.ToCustomerItem();
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(item);

        AssertSql();
    }

    [Fact]
    public async Task
        SaveChangesAsync_ModifiedScalar_TransitionsToUnchangedAndRefreshesOriginalValues()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#MOD-5",
            Version = 1,
            Email = "initial+5@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero),
            NullableNote = null,
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Email = "post-save@example.com";
        var entry = Db.Entry(item);
        entry.State.Should().Be(EntityState.Modified);

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        entry.State.Should().Be(EntityState.Unchanged);
        entry.Property(x => x.Email).OriginalValue.Should().Be("post-save@example.com");

        var actual = (await GetItemAsync(item.Pk, item.Sk, CancellationToken))?.ToCustomerItem();
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(item);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "Email" = ?
            WHERE "Pk" = ? AND "Sk" = ? AND "Version" = ?
            """);
    }
}
