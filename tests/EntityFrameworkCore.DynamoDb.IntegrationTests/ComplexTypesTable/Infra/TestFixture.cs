using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ComplexTypesTable;

public abstract class ComplexCollectionWithIdPropertyTestFixture : DynamoTestFixtureBase
{
    protected ComplexCollectionWithIdPropertyTestFixture(DynamoContainerFixture fixture) :
        base(fixture)
        => EnsureClassTableInitialized(
            AnalysisReportTable.TableName,
            AnalysisReportTable.CreateTable);

    public ComplexCollectionWithIdPropertyDbContext Db
    {
        get
        {
            field ??= new ComplexCollectionWithIdPropertyDbContext(
                CreateOptions<ComplexCollectionWithIdPropertyDbContext>(options
                    => options.DynamoDbClient(Client)));
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

public abstract class ConventionOnlyCollectionShapeTableTestFixture : DynamoTestFixtureBase
{
    protected ConventionOnlyCollectionShapeTableTestFixture(DynamoContainerFixture fixture) :
        base(fixture)
        => EnsureClassTableInitialized(
            CollectionShapeItemTable.TableName,
            CollectionShapeItemTable.CreateTable);

    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public ConventionOnlyCollectionShapeDbContext Db
    {
        get
        {
            field ??= new ConventionOnlyCollectionShapeDbContext(
                CreateOptions<ConventionOnlyCollectionShapeDbContext>(options
                    => options.DynamoDbClient(Client)));
            return field;
        }
    }
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
                CreateOptions<ComplexTypesTableDbContext>(options
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

public abstract class ComplexTypeDiscriminatorTableTestFixture : DynamoTestFixtureBase
{
    protected ComplexTypeDiscriminatorTableTestFixture(DynamoContainerFixture fixture) :
        base(fixture)
        => EnsureClassTableInitialized(
            ComplexTypeDiscriminatorTable.TableName,
            ComplexTypeDiscriminatorTable.CreateTable);

    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public ComplexTypeDiscriminatorDbContext Db
    {
        get
        {
            field ??= CreateContext();
            return field;
        }
    }

    protected ComplexTypeDiscriminatorDbContext CreateContext()
        => new(
            CreateOptions<ComplexTypeDiscriminatorDbContext>(options
                => options.DynamoDbClient(Client)));
}

public abstract class ClrDiscriminatorTableTestFixture : DynamoTestFixtureBase
{
    protected ClrDiscriminatorTableTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(
            ClrDiscriminatorTable.TableName,
            ClrDiscriminatorTable.CreateTable);

    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public ClrDiscriminatorDbContext Db
    {
        get
        {
            field ??= CreateContext();
            return field;
        }
    }

    protected ClrDiscriminatorDbContext CreateContext()
        => new(CreateOptions<ClrDiscriminatorDbContext>(options => options.DynamoDbClient(Client)));
}

public abstract class ProjectionComplexTableTestFixture : DynamoTestFixtureBase
{
    protected ProjectionComplexTableTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(
            ProjectionComplexTable.TableName,
            ProjectionComplexTable.CreateTable);

    protected TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    public ProjectionComplexDbContext Db
    {
        get
        {
            field ??= CreateContext();
            return field;
        }
    }

    protected ProjectionComplexDbContext CreateContext()
        => new(
            CreateOptions<ProjectionComplexDbContext>(options => options.DynamoDbClient(Client)));
}
