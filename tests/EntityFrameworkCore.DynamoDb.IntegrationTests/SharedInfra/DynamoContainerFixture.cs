using Amazon.DynamoDBv2;
using Amazon.Runtime;
using EntityFrameworkCore.DynamoDb.TestUtilities;
using JetBrains.Annotations;
using Testcontainers.DynamoDb;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

[UsedImplicitly]
public sealed class DynamoContainerFixture(IMessageSink messageSink)
    : ContainerFixture<DynamoDbBuilder, DynamoDbContainer>(messageSink)
{
    public DynamoMapperRegistry Mappers { get; } =
        DynamoMapperRegistry.FromAssembly(typeof(DynamoContainerFixture).Assembly);

    public IAmazonDynamoDB Client
    {
        get
        {
            field ??= new AmazonDynamoDBClient(
                new BasicAWSCredentials("test", "test"),
                new AmazonDynamoDBConfig { ServiceURL = Container.GetConnectionString() });
            return field;
        }
    }

    protected override DynamoDbBuilder Configure() => new(DynamoDbLocalImage.Name);
}
