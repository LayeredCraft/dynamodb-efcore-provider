using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

public class HasKeyOnlyTableKeyTests(DynamoContainerFixture fixture) : PkSkTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasKeyOnlyContext_ResolvesTableKeyRolesFromEfPrimaryKey()
    {
        var entityType = HasKeyOnlyDb.Model.FindEntityType(typeof(PkSkItem))!;
        var efPrimaryKey = entityType.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray();

        entityType.GetPartitionKeyPropertyName().Should().Be(nameof(PkSkItem.Pk));
        entityType.GetSortKeyPropertyName().Should().Be(nameof(PkSkItem.Sk));
        efPrimaryKey.Should().Equal(nameof(PkSkItem.Pk), nameof(PkSkItem.Sk));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task HasKeyOnlyContext_QueryByFullKey_RoundTripsPersistedItem()
    {
        var result = await HasKeyOnlyDb
            .Items
            .Where(item => item.Pk == "P#1" && item.Sk == "0002")
            .SingleAsync(CancellationToken);

        var expected = PkSkItems.Items.Single(item => item.Pk == "P#1" && item.Sk == "0002");
        result.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" = 'P#1' AND "sk" = '0002'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task HasKeyOnlyContext_OrderBySortKeyWithinPartition_RoundTripsPersistedItems()
    {
        var results = await HasKeyOnlyDb
            .Items
            .Where(item => item.Pk == "P#1")
            .OrderBy(item => item.Sk)
            .ToListAsync(CancellationToken);

        var expected =
            PkSkItems.Items.Where(item => item.Pk == "P#1").OrderBy(item => item.Sk).ToList();
        results.Should().Equal(expected);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" = 'P#1'
            ORDER BY "sk" ASC
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task HasKeyOnlyContext_SaveChanges_InsertUpdateDelete_RoundTripsPersistedItem()
    {
        var item = new PkSkItem
        {
            Pk = "P#has-key-only-save", Sk = "0001", Category = "created", IsTarget = false
        };

        HasKeyOnlyDb.Items.Add(item);
        await HasKeyOnlyDb.SaveChangesAsync(CancellationToken);
        await AssertItemExistsInDynamoDbAsync(item, CancellationToken);

        item.Category = "updated";
        item.IsTarget = true;
        await HasKeyOnlyDb.SaveChangesAsync(CancellationToken);
        await AssertItemExistsInDynamoDbAsync(item, CancellationToken);

        HasKeyOnlyDb.Items.Remove(item);
        await HasKeyOnlyDb.SaveChangesAsync(CancellationToken);
        await AssertItemDoesNotExistAsync(item.Pk, item.Sk, CancellationToken);

        AssertSql(
            """
            INSERT INTO "PkSkItems"
            VALUE {'pk': ?, 'sk': ?, 'category': ?, 'isTarget': ?}
            """,
            """
            UPDATE "PkSkItems"
            SET "category" = ?, "isTarget" = ?
            WHERE "pk" = ? AND "sk" = ?
            """,
            """
            DELETE FROM "PkSkItems"
            WHERE "pk" = ? AND "sk" = ?
            """);
    }
}
