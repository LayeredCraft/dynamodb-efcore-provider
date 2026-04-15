using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

public class ConvertedCollectionAndEscapingSaveChangesTests(DynamoContainerFixture fixture)
    : SaveChangesTableTestFixture(fixture)
{
    [Fact]
    public async Task SaveChangesAsync_AddedConvertedCollectionProperty_StoresAsScalar()
    {
        var item = new ConvertedCollectionItem
        {
            Pk = "TENANT#CONV", Sk = "COLL#ADD-1", Version = 1, Scores = [1, 2, 3],
        };

        Db.ConvertedCollectionItems.Add(item);
        await Db.SaveChangesAsync(CancellationToken);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw.Should().NotBeNull();
        raw!["Scores"].S.Should().Be("1|2|3");

        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'Scores': ?, 'Version': ?}
            """);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedConvertedCollectionProperty_PersistsAndEmitsUpdate()
    {
        var item = new ConvertedCollectionItem
        {
            Pk = "TENANT#CONV", Sk = "COLL#MOD-1", Version = 1, Scores = [5],
        };

        Db.ConvertedCollectionItems.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Scores = [8, 13, 21];

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw.Should().NotBeNull();
        raw!["Scores"].S.Should().Be("8|13|21");

        AssertSql(
            """
            UPDATE "AppItems"
            SET "Scores" = ?
            WHERE "Pk" = ? AND "Sk" = ? AND "Version" = ?
            """);
    }

    [Fact]
    public async Task SaveChangesAsync_InsertAttributeNameWithSingleQuote_EscapesStringLiteral()
    {
        var item = new QuotedAttributeItem
        {
            Pk = "TENANT#ESC", Sk = "QUOTE#1", Version = 1, DisplayName = "Ada",
        };

        Db.QuotedAttributeItems.Add(item);
        await Db.SaveChangesAsync(CancellationToken);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw.Should().NotBeNull();
        raw!.Should().ContainKey("O'Brien");
        raw["O'Brien"].S.Should().Be("Ada");

        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'O''Brien': ?, 'Version': ?}
            """);
    }

    [Fact]
    public async Task
        SaveChangesAsync_UpdateAttributeNameWithSingleQuote_EmitsDoubleQuotedIdentifier()
    {
        // Attribute names in UPDATE SET / WHERE clauses use double-quoted identifiers.
        // A single-quote inside a double-quoted identifier is valid as-is — no escaping needed.
        var item = new QuotedAttributeItem
        {
            Pk = "TENANT#ESC", Sk = "QUOTE#UPDATE-1", Version = 1, DisplayName = "Ada",
        };

        Db.QuotedAttributeItems.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.DisplayName = "Grace";
        var affected = await Db.SaveChangesAsync(CancellationToken);

        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw.Should().NotBeNull();
        raw!.Should().ContainKey("O'Brien");
        raw["O'Brien"].S.Should().Be("Grace");

        AssertSql(
            """
            UPDATE "AppItems"
            SET "O'Brien" = ?
            WHERE "Pk" = ? AND "Sk" = ? AND "Version" = ?
            """);
    }

    [Fact]
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

        Db.QuotedAttributeItems.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        Db.QuotedAttributeItems.Remove(item);
        var affected = await Db.SaveChangesAsync(CancellationToken);

        affected.Should().Be(1);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw.Should().BeNull();

        AssertSql(
            """
            DELETE FROM "AppItems"
            WHERE "Pk" = ? AND "Sk" = ? AND "Version" = ?
            """);
    }
}
