using Amazon.DynamoDBv2;
using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;
using Testcontainers.DynamoDb;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>Represents the DynamoFixture type.</summary>
public class DynamoFixture : IAsyncLifetime, IDynamoDbTestFixture
{
    /// <summary>Provides functionality for this member.</summary>
    public IAmazonDynamoDB Client
    {
        get
        {
            field ??= new AmazonDynamoDBClient(
                new AmazonDynamoDBConfig { ServiceURL = Container.GetConnectionString() });
            return field;
        }
    }

    /// <summary>Provides functionality for this member.</summary>
    public DynamoDbContainer Container { get; }

    /// <summary>Provides functionality for this member.</summary>
    public DynamoFixture()
        => Container =
            new DynamoDbBuilder("amazon/dynamodb-local:latest").Build()
            ?? throw new Exception("Failed to create DynamoDB Container");

    /// <summary>Provides functionality for this member.</summary>
    public virtual async ValueTask InitializeAsync() => await Container.StartAsync();

    /// <summary>Provides functionality for this member.</summary>
    public virtual async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Represents the SimpleTableDynamoFixture type.</summary>
public class SimpleTableDynamoFixture : DynamoFixture
{
    /// <summary>Provides functionality for this member.</summary>
    public const string TableName = "SimpleItems";
}
