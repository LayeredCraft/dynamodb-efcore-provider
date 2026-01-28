using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Testcontainers.DynamoDb;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class DynamoFixture : IAsyncLifetime
{
    public IAmazonDynamoDB Client
    {
        get
        {
            field ??= new AmazonDynamoDBClient(
                new AmazonDynamoDBConfig { ServiceURL = Container.GetConnectionString() });
            return field;
        }
    }

    public DynamoDbContainer Container { get; }

    public DynamoFixture()
        => Container =
            new DynamoDbBuilder("amazon/dynamodb-local:latest").Build()
            ?? throw new Exception("Failed to create DynamoDB Container");

    public virtual async ValueTask InitializeAsync() => await Container.StartAsync();

    public virtual async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

public class SimpleTableDynamoFixture : DynamoFixture
{
    public const string TableName = "SimpleItems";

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        // create table
        await Client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = TableName,
                AttributeDefinitions =
                [
                    new AttributeDefinition
                    {
                        AttributeName = "Pk", AttributeType = ScalarAttributeType.S,
                    },
                ],
                KeySchema =
                [
                    new KeySchemaElement { AttributeName = "Pk", KeyType = KeyType.HASH },
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            });

        // seed data
        await Client.BatchWriteItemAsync(
            new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    [TableName] =
                        SimpleItem
                            .GetSampleData()
                            .Select(item => new WriteRequest
                            {
                                PutRequest = new PutRequest { Item = item },
                            })
                            .ToList(),
                },
            });
    }

    public override async ValueTask DisposeAsync()
    {
        await Client.DeleteTableAsync(TableName);
        await base.DisposeAsync();
    }
}
