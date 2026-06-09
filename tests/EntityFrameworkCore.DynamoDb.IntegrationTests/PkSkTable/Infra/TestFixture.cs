using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

public abstract class PkSkTableTestFixture : DynamoTestFixtureBase
{
    protected PkSkTableTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(PkSkItemTable.TableName, PkSkItemTable.CreateTable);

    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public PkSkTableDbContext Db
    {
        get
        {
            field ??= new PkSkTableDbContext(
                CreateOptions<PkSkTableDbContext>(options => options.DynamoDbClient(Client)));
            return field;
        }
    }

    public PkSkHasKeyOnlyDbContext HasKeyOnlyDb
    {
        get
        {
            field ??= new PkSkHasKeyOnlyDbContext(
                CreateOptions<PkSkHasKeyOnlyDbContext>(options => options.DynamoDbClient(Client)));
            return field;
        }
    }

    protected Task AssertItemExistsInDynamoDbAsync(
        PkSkItem item,
        CancellationToken cancellationToken)
        => AssertItemExistsInDynamoDbAsync(
            item,
            PkSkItemTable.TableName,
            CreateKey(item.Pk, item.Sk),
            cancellationToken);

    protected async Task AssertItemDoesNotExistAsync(
        string pk,
        string sk,
        CancellationToken cancellationToken)
        => (await GetRawItemAsync(pk, sk, cancellationToken)).Should().BeNull();

    protected async Task<Dictionary<string, AttributeValue>?> GetRawItemAsync(
        string pk,
        string sk,
        CancellationToken cancellationToken)
    {
        var response = await Client.GetItemAsync(
            new GetItemRequest { TableName = PkSkItemTable.TableName, Key = CreateKey(pk, sk) },
            cancellationToken);

        return response.Item.Count == 0 ? null : response.Item;
    }

    protected Task DeleteRawItemAsync(string pk, string sk, CancellationToken cancellationToken)
        => Client.DeleteItemAsync(
            new DeleteItemRequest { TableName = PkSkItemTable.TableName, Key = CreateKey(pk, sk) },
            cancellationToken);

    private static Dictionary<string, AttributeValue> CreateKey(string pk, string sk)
        => new() { ["pk"] = pk.ToAttributeValue(), ["sk"] = sk.ToAttributeValue() };
}
