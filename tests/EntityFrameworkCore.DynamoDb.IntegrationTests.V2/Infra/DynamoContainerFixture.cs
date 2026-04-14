using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;
using JetBrains.Annotations;
using Testcontainers.DynamoDb;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.Infra;

[UsedImplicitly]
public sealed class DynamoContainerFixture(IMessageSink messageSink)
    : ContainerFixture<DynamoDbBuilder, DynamoDbContainer>(messageSink)
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

    protected override DynamoDbBuilder Configure() => new("amazon/dynamodb-local:latest");

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        await SimpleItemTable.CreateTable(Client, TestContext.Current.CancellationToken);
    }
}
