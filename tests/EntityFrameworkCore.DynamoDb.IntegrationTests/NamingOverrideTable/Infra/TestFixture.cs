using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingOverrideTable.Infra;

public abstract class NamingOverridesTableTestFixture : DynamoTestFixtureBase
{
    protected NamingOverridesTableTestFixture(DynamoContainerFixture container) : base(container)
        => EnsureClassTableInitialized(
            NamingOverridesItemTable.TableName,
            NamingOverridesItemTable.CreateTable);

    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public NamingOverridesTableDbContext Db
    {
        get
        {
            field ??= new NamingOverridesTableDbContext(
                CreateOptions<NamingOverridesTableDbContext>(options
                    => options.DynamoDbClient(Client)));
            return field;
        }
    }

    protected Task PutItemAsync(
        Dictionary<string, AttributeValue> item,
        CancellationToken cancellationToken)
        => Client.PutItemAsync(
            new PutItemRequest { TableName = NamingOverridesItemTable.TableName, Item = item },
            cancellationToken);
}
