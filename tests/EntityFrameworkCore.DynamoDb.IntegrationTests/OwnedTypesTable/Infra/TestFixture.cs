using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

public abstract class OwnedCollectionWithIdPropertyTestFixture : DynamoTestFixtureBase
{
    protected OwnedCollectionWithIdPropertyTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(
            AnalysisReportTable.TableName,
            AnalysisReportTable.CreateTable);

    public OwnedCollectionWithIdPropertyDbContext Db
    {
        get
        {
            field ??= new OwnedCollectionWithIdPropertyDbContext(
                CreateOptions<OwnedCollectionWithIdPropertyDbContext>(
                    options => options.DynamoDbClient(Client)));
            return field;
        }
    }
}

public abstract class OwnedTypesTableTestFixture : DynamoTestFixtureBase
{
    protected OwnedTypesTableTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(
            OwnedTypesItemTable.TableName,
            OwnedTypesItemTable.CreateTable);

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
