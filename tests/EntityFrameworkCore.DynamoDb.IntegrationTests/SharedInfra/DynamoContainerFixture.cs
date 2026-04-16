using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.IntegrationTests.CompetingGsiTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventions.Infra;
using EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexProjectionTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable.SharedTableWithIndexes;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;
using JetBrains.Annotations;
using Testcontainers.DynamoDb;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

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
        await NamingConventionsItemTable.CreateTable(Client, ct);
    }
}
