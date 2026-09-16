using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using EntityFrameworkCore.DynamoDb.TestUtilities;
using Testcontainers.DynamoDb;
using Xunit;
using Xunit.v3;

namespace EntityFrameworkCore.DynamoDb.NativeAotTests;

public sealed class DynamoFixture : IAsyncLifetime
{
    internal const string ServiceUrlEnvironmentVariable = "DYNAMO_AOT_TEST_URL";
    private static int _containerStarts;
    private DynamoDbContainer? _container;
    private AmazonDynamoDBClient? _client;

    public DynamoFixture(ITestContextAccessor accessor) { }

    internal DynamoFixture() { }

    public static int ContainerStartCount => Volatile.Read(ref _containerStarts);

    public IAmazonDynamoDB Client
        => _client
            ?? throw new InvalidOperationException(
                "The DynamoDB fixture has not been initialized.");

    public async ValueTask InitializeAsync()
    {
        var starts = Interlocked.Increment(ref _containerStarts);
        if (starts != 1)
            throw new InvalidOperationException(
                $"Expected one DynamoDB container start, but observed {starts}.");

        try
        {
            _container = new DynamoDbBuilder(DynamoDbLocalImage.Name).Build();
            await _container.StartAsync();
            _client = new AmazonDynamoDBClient(
                new BasicAWSCredentials("local", "local"),
                new AmazonDynamoDBConfig
                {
                    ServiceURL = _container.GetConnectionString(),
                    AuthenticationRegion = "us-east-1"
                });

            Environment.SetEnvironmentVariable(
                ServiceUrlEnvironmentVariable,
                _container.GetConnectionString());
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "local");
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "local");
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _client = null;

        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }
    }

    public async Task ResetAndSeedAsync(
        CancellationToken cancellationToken = default,
        string? tableName = null)
    {
        tableName ??= AotRuntimeContext.TableName;
        await RecreateTableAsync(tableName, cancellationToken);

        await using var context = new AotRuntimeContext();
        context.AddRange(AotRuntimeData.CreateSeedItems());
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SeedRuntimeMappedItemAsync(CancellationToken cancellationToken = default)
        => await Client.PutItemAsync(
            new PutItemRequest
            {
                TableName = AotRuntimeContext.TableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new() { S = "runtime-tenant" },
                    ["sk"] = new() { S = "runtime-item" },
                    ["name"] = new() { S = "RuntimeConfigured" },
                    ["$type"] = new() { S = nameof(AotRuntimeItem) }
                }
            },
            cancellationToken);

    public async Task<string?> BootstrapNextTokenAsync(
        string partitionKey,
        int pageSize,
        string? seedToken,
        CancellationToken cancellationToken = default)
    {
        var response = await Client.ExecuteStatementAsync(
            new ExecuteStatementRequest
            {
                Statement =
                    $"SELECT \"pk\", \"sk\" FROM \"{AotRuntimeContext.TableName}\" WHERE \"pk\" = ?",
                Parameters = [new AttributeValue { S = partitionKey }],
                Limit = pageSize,
                NextToken = seedToken
            },
            cancellationToken);

        return response.NextToken;
    }

    public static string? ExtractDynamoDbLocalContinuationKey(string? token)
    {
        if (token is null)
            return null;

        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token));
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.GetProperty("opIndexToExclusiveNextKey").GetRawText();
    }

    private async Task RecreateTableAsync(string tableName, CancellationToken cancellationToken)
    {
        try
        {
            await Client.DeleteTableAsync(tableName, cancellationToken);
            await WaitForTableDeletedAsync(tableName, cancellationToken);
        }
        catch (ResourceNotFoundException) { }

        await Client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = tableName,
                BillingMode = BillingMode.PAY_PER_REQUEST,
                AttributeDefinitions =
                [
                    new AttributeDefinition("pk", ScalarAttributeType.S),
                    new AttributeDefinition("sk", ScalarAttributeType.S)
                ],
                KeySchema =
                [
                    new KeySchemaElement("pk", KeyType.HASH),
                    new KeySchemaElement("sk", KeyType.RANGE)
                ]
            },
            cancellationToken);

        while ((await Client.DescribeTableAsync(tableName, cancellationToken)).Table.TableStatus
            != TableStatus.ACTIVE)
            await Task.Delay(100, cancellationToken);
    }

    private async Task WaitForTableDeletedAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        while (true)
            try
            {
                await Client.DescribeTableAsync(tableName, cancellationToken);
                await Task.Delay(100, cancellationToken);
            }
            catch (ResourceNotFoundException)
            {
                return;
            }
    }
}

public sealed class DynamoFixtureRegistration : EngineInitializationAttribute
{
    public override ValueTask InitializeAsync()
    {
        RegisteredEngineConfig.RegisterAssemblyFixtureFactory(
            typeof(DynamoFixture),
            static _ => new ValueTask<object?>(new DynamoFixture()));
        return default;
    }
}
