using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

public abstract class OwnedTypesTableTestFixture(DynamoContainerFixture fixture)
    : DynamoTestFixtureBase(fixture), IClassFixture<DynamoContainerFixture>
{
    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public OwnedTypesTableDbContext Db
    {
        get
        {
            field ??= new OwnedTypesTableDbContext(
                CreateOptions<OwnedTypesTableDbContext>(options => options.DynamoDbClient(Client)));
            return field;
        }
    }

    protected Task PutItemAsync(
        Dictionary<string, AttributeValue> item,
        CancellationToken cancellationToken)
        => Client.PutItemAsync(
            new PutItemRequest { TableName = OwnedTypesItemTable.TableName, Item = item },
            cancellationToken);
}
