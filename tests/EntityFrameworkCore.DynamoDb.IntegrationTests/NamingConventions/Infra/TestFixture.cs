using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventions.Infra;

public abstract class NamingConventionsTableTestFixture : DynamoTestFixtureBase
{
    protected NamingConventionsTableTestFixture(DynamoContainerFixture container) : base(container)
        => EnsureClassTableInitialized(
            NamingConventionsItemTable.TableName,
            NamingConventionsItemTable.CreateTable);
    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public NamingConventionsTableDbContext Db
    {
        get
        {
            field ??= new NamingConventionsTableDbContext(
                CreateOptions<NamingConventionsTableDbContext>(options => options.DynamoDbClient(Client)));
            return field;
        }
    }

    protected Task PutItemAsync(
        Dictionary<string, AttributeValue> item,
        CancellationToken cancellationToken)
        => Client.PutItemAsync(
            new PutItemRequest { TableName = NamingConventionsItemTable.TableName, Item = item },
            cancellationToken);
}
