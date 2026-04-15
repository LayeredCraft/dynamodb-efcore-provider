using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.IntegrationTests.CompetingGsiTable;
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
}
