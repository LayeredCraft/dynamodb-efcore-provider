using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SimpleTable;
using JetBrains.Annotations;
using Testcontainers.DynamoDb;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;

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

        var ct = TestContext.Current.CancellationToken;
        await SimpleItemTable.CreateTable(Client, ct);
        await SharedItemTable.CreateTable(Client, ct);
    }
}
