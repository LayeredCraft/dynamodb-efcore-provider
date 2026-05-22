using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

public abstract class SaveChangesTableTestFixture : DynamoTestFixtureBase
{
    protected SaveChangesTableTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(
            SaveChangesItemTable.TableName,
            SaveChangesItemTable.CreateTable);

    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public SaveChangesTableDbContext Db
    {
        get
        {
            field ??= new SaveChangesTableDbContext(
                CreateOptions<SaveChangesTableDbContext>(o => o.DynamoDbClient(Client)));
            return field;
        }
    }

    protected Task PutItemAsync(
        Dictionary<string, AttributeValue> item,
        CancellationToken cancellationToken)
        => Client.PutItemAsync(
            new PutItemRequest { TableName = SaveChangesItemTable.TableName, Item = item },
            cancellationToken);

    protected Task AssertItemExistsInDynamoDbAsync<T>(
        T item,
        string pk,
        string sk,
        CancellationToken cancellationToken)
        => AssertItemExistsInDynamoDbAsync(
            item,
            SaveChangesItemTable.TableName,
            new Dictionary<string, AttributeValue>
            {
                ["pk"] = pk.ToAttributeValue(), ["sk"] = sk.ToAttributeValue()
            },
            cancellationToken);

    protected async Task AssertItemDoesNotExistAsync(
        string pk,
        string sk,
        CancellationToken cancellationToken)
        => (await GetItemAsync(pk, sk, cancellationToken)).Should().BeNull();

    protected async Task<Dictionary<string, AttributeValue>> GetExistingItemAsync(
        string pk,
        string sk,
        CancellationToken cancellationToken)
        => await GetItemAsync(pk, sk, cancellationToken)
            ?? throw new InvalidOperationException($"Item {pk}/{sk} not found.");

    protected async Task AssertRawItemAsync(
        string pk,
        string sk,
        Action<Dictionary<string, AttributeValue>> assertion,
        CancellationToken cancellationToken)
        => assertion(await GetExistingItemAsync(pk, sk, cancellationToken));

    protected async Task AssertRawAttributeAsync(
        string pk,
        string sk,
        string attributeName,
        Action<AttributeValue> assertion,
        CancellationToken cancellationToken)
    {
        var item = await GetExistingItemAsync(pk, sk, cancellationToken);
        item.Should().ContainKey(attributeName);
        assertion(item[attributeName]);
    }

    protected Task AssertRawStringAsync(
        string pk,
        string sk,
        string attributeName,
        string expected,
        CancellationToken cancellationToken)
        => AssertRawAttributeAsync(
            pk,
            sk,
            attributeName,
            attribute => attribute.S.Should().Be(expected),
            cancellationToken);

    protected Task AssertRawNumberAsync(
        string pk,
        string sk,
        string attributeName,
        string expected,
        CancellationToken cancellationToken)
        => AssertRawAttributeAsync(
            pk,
            sk,
            attributeName,
            attribute => attribute.N.Should().Be(expected),
            cancellationToken);

    protected Task AssertRawNullAsync(
        string pk,
        string sk,
        string attributeName,
        CancellationToken cancellationToken)
        => AssertRawAttributeAsync(
            pk,
            sk,
            attributeName,
            attribute => attribute.NULL.Should().BeTrue(),
            cancellationToken);

    protected async Task AssertRawMissingAsync(
        string pk,
        string sk,
        string attributeName,
        CancellationToken cancellationToken)
    {
        var item = await GetExistingItemAsync(pk, sk, cancellationToken);
        item.Should().NotContainKey(attributeName);
    }

    protected Task AssertRawStringSetAsync(
        string pk,
        string sk,
        string attributeName,
        IEnumerable<string> expected,
        CancellationToken cancellationToken)
        => AssertRawAttributeAsync(
            pk,
            sk,
            attributeName,
            attribute => attribute.SS.Should().BeEquivalentTo(expected),
            cancellationToken);

    protected async Task SaveAndClearLogAsync()
    {
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();
    }

    protected async Task SeedAsync<T>(T entity) where T : class
    {
        Db.Set<T>().Add(entity);
        await SaveAndClearLogAsync();
    }

    protected async Task SaveChangesShouldAffectAsync(int expected)
        => (await Db.SaveChangesAsync(CancellationToken)).Should().Be(expected);

    protected async Task SaveChangesShouldAffectAsync(bool acceptAllChangesOnSuccess, int expected)
        => (await Db.SaveChangesAsync(acceptAllChangesOnSuccess, CancellationToken))
            .Should()
            .Be(expected);

    protected void AssertEntryState<T>(T entity, EntityState expected) where T : class
        => AssertEntryState(Db, entity, expected);

    protected static void AssertEntryState<T>(DbContext context, T entity, EntityState expected)
        where T : class
        => context.Entry(entity).State.Should().Be(expected);

    protected void AssertEntryStates(params (object Entity, EntityState Expected)[] entries)
        => AssertEntryStates(Db, entries);

    protected static void AssertEntryStates(
        DbContext context,
        params (object Entity, EntityState Expected)[] entries)
    {
        foreach (var (entity, expected) in entries)
            context.Entry(entity).State.Should().Be(expected);
    }

    protected void AssertOriginalValue<TEntity, TProperty>(
        TEntity entity,
        string propertyName,
        TProperty expected) where TEntity : class
        => Db.Entry(entity).Property<TProperty>(propertyName).OriginalValue.Should().Be(expected);

    protected async Task<Dictionary<string, AttributeValue>?> GetItemAsync(
        string pk,
        string sk,
        CancellationToken cancellationToken)
    {
        var response = await Client.GetItemAsync(
            new GetItemRequest
            {
                TableName = SaveChangesItemTable.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new() { S = pk }, ["sk"] = new() { S = sk }
                }
            },
            cancellationToken);

        return response.Item is { Count: > 0 } item ? item : null;
    }

    protected async Task BumpVersionAsync(string pk, string sk, CancellationToken ct)
    {
        var item = await GetItemAsync(pk, sk, ct)
            ?? throw new InvalidOperationException(
                $"Cannot bump version: item {pk}/{sk} not found.");

        var currentVersion = long.Parse(item["version"].N);
        item["version"] = new AttributeValue { N = (currentVersion + 1).ToString() };
        await PutItemAsync(item, ct);
    }
}
