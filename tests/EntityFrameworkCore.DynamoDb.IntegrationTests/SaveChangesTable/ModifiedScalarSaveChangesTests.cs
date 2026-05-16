using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

public class ModifiedScalarSaveChangesTests(DynamoContainerFixture fixture)
    : SaveChangesTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
        await SeedAsync(item);

        item.Email = "updated+1@example.com";

        await SaveChangesShouldAffectAsync(1);

        await AssertItemExistsInDynamoDbAsync(item, item.Pk, item.Sk, CancellationToken);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "email" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
        await SeedAsync(item);

        item.Email = "updated+2@example.com";
        item.IsPreferred = true;

        await SaveChangesShouldAffectAsync(1);

        await AssertItemExistsInDynamoDbAsync(item, item.Pk, item.Sk, CancellationToken);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "email" = ?, "isPreferred" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
        await SeedAsync(item);

        item.NullableNote = null;

        await SaveChangesShouldAffectAsync(1);

        await AssertItemExistsInDynamoDbAsync(item, item.Pk, item.Sk, CancellationToken);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "nullableNote" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
        await SeedAsync(item);

        await SaveChangesShouldAffectAsync(0);

        await AssertItemExistsInDynamoDbAsync(item, item.Pk, item.Sk, CancellationToken);

        AssertSql();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
        await SeedAsync(item);

        item.Email = "post-save@example.com";
        var entry = Db.Entry(item);
        entry.State.Should().Be(EntityState.Modified);

        await SaveChangesShouldAffectAsync(1);

        entry.State.Should().Be(EntityState.Unchanged);
        AssertOriginalValue(item, nameof(CustomerItem.Email), "post-save@example.com");

        await AssertItemExistsInDynamoDbAsync(item, item.Pk, item.Sk, CancellationToken);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "email" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }
}
