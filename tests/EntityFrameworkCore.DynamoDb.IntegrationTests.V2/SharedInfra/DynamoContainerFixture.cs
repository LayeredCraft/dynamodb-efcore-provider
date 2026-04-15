using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.CompetingGsiTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.OwnedTypesTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.PkSkTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.PrimitiveCollectionsTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SaveChangesTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SecondaryIndexProjectionTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SecondaryIndexTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedTable.SharedTableWithIndexes;
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
        await SharedTableWithIndexesItemTable.CreateTable(Client, ct);
        await CompetingGsiOrdersTable.CreateTable(Client, ct);
        await OwnedTypesItemTable.CreateTable(Client, ct);
        await PkSkItemTable.CreateTable(Client, ct);
        await PrimitiveCollectionsItemTable.CreateTable(Client, ct);
        await SaveChangesItemTable.CreateTable(Client, ct);
        await SecondaryIndexOrdersTable.CreateTable(Client, ct);
        await SecondaryIndexProjectionOrdersTable.CreateTable(Client, ct);
    }
}
