using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ComplexTypesTable;

public abstract class ComplexCollectionWithIdPropertyTestFixture : DynamoTestFixtureBase
{
    protected ComplexCollectionWithIdPropertyTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(
            AnalysisReportTable.TableName,
            AnalysisReportTable.CreateTable);

    public ComplexCollectionWithIdPropertyDbContext Db
    {
        get
        {
            field ??= new ComplexCollectionWithIdPropertyDbContext(
                CreateOptions<ComplexCollectionWithIdPropertyDbContext>(
                    options => options.DynamoDbClient(Client)));
            return field;
        }
    }
}

public abstract class ConventionOnlyComplexTypesTableTestFixture : DynamoTestFixtureBase
{
    protected ConventionOnlyComplexTypesTableTestFixture(DynamoContainerFixture fixture) :
        base(fixture)
        => EnsureClassTableInitialized(
            ComplexTypesItemTable.TableName,
            ComplexTypesItemTable.CreateTable);

    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public ConventionOnlyComplexTypesDbContext Db
    {
        get
        {
            field ??= new ConventionOnlyComplexTypesDbContext(
                CreateOptions<ConventionOnlyComplexTypesDbContext>(options
                    => options.DynamoDbClient(Client)));
            return field;
        }
    }

    protected Task PutItemAsync(
        Dictionary<string, AttributeValue> item,
        CancellationToken cancellationToken)
        => Client.PutItemAsync(
            new PutItemRequest { TableName = ComplexTypesItemTable.TableName, Item = item },
            cancellationToken);
}

public abstract class ComplexTypesTableTestFixture : DynamoTestFixtureBase
{
    protected ComplexTypesTableTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(
            ComplexTypesItemTable.TableName,
            ComplexTypesItemTable.CreateTable);

    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public ComplexTypesTableDbContext Db
    {
        get
        {
            field ??= new ComplexTypesTableDbContext(
                CreateOptions<ComplexTypesTableDbContext>(options => options.DynamoDbClient(Client)));
            return field;
        }
    }

    protected Task PutItemAsync(
        Dictionary<string, AttributeValue> item,
        CancellationToken cancellationToken)
        => Client.PutItemAsync(
            new PutItemRequest { TableName = ComplexTypesItemTable.TableName, Item = item },
            cancellationToken);
}
