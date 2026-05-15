using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

public class ConvertedCollectionAndEscapingSaveChangesTests(DynamoContainerFixture fixture)
    : SaveChangesTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_AddedConvertedCollectionProperty_StoresAsScalar()
    {
        var item = new ConvertedCollectionItem
        {
            Pk = "TENANT#CONV", Sk = "COLL#ADD-1", Version = 1, Scores = [1, 2, 3],
        };

        Db.ConvertedCollectionItems.Add(item);
        await Db.SaveChangesAsync(CancellationToken);

        await AssertRawStringAsync(item.Pk, item.Sk, "scores", "1|2|3", CancellationToken);

        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'scores': ?, 'version': ?}
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_ModifiedConvertedCollectionProperty_PersistsAndEmitsUpdate()
    {
        var item = new ConvertedCollectionItem
        {
            Pk = "TENANT#CONV", Sk = "COLL#MOD-1", Version = 1, Scores = [5],
        };

        await SeedAsync(item);

        item.Scores = [8, 13, 21];

        await SaveChangesShouldAffectAsync(1);

        await AssertRawStringAsync(item.Pk, item.Sk, "scores", "8|13|21", CancellationToken);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "scores" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_InsertAttributeNameWithSingleQuote_EscapesStringLiteral()
    {
        var item = new QuotedAttributeItem
        {
            Pk = "TENANT#ESC", Sk = "QUOTE#1", Version = 1, DisplayName = "Ada",
        };

        Db.QuotedAttributeItems.Add(item);
        await Db.SaveChangesAsync(CancellationToken);

        await AssertRawStringAsync(item.Pk, item.Sk, "O'Brien", "Ada", CancellationToken);

        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'pk': ?, 'sk': ?, '$type': ?, 'O''Brien': ?, 'version': ?}
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        SaveChangesAsync_UpdateAttributeNameWithSingleQuote_EmitsDoubleQuotedIdentifier()
    {
        // Attribute names in UPDATE SET / WHERE clauses use double-quoted identifiers.
        // A single-quote inside a double-quoted identifier is valid as-is — no escaping needed.
        var item = new QuotedAttributeItem
        {
            Pk = "TENANT#ESC", Sk = "QUOTE#UPDATE-1", Version = 1, DisplayName = "Ada",
        };

        await SeedAsync(item);

        item.DisplayName = "Grace";
        await SaveChangesShouldAffectAsync(1);

        await AssertRawStringAsync(item.Pk, item.Sk, "O'Brien", "Grace", CancellationToken);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "O'Brien" = ?
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        SaveChangesAsync_DeleteAttributeNameWithSingleQuote_EmitsDoubleQuotedIdentifier()
    {
        // Verify that DELETE WHERE conditions for concurrency tokens use double-quoted identifiers,
        // keeping single-quote attribute names unescaped (they are valid inside double-quote
        // syntax).
        var item = new QuotedAttributeItem
        {
            Pk = "TENANT#ESC", Sk = "QUOTE#DELETE-1", Version = 1, DisplayName = "Ada",
        };

        await SeedAsync(item);

        Db.QuotedAttributeItems.Remove(item);
        await SaveChangesShouldAffectAsync(1);

        await AssertItemDoesNotExistAsync(item.Pk, item.Sk, CancellationToken);

        AssertSql(
            """
            DELETE FROM "AppItems"
            WHERE "pk" = ? AND "sk" = ? AND "version" = ?
            """);
    }
}
