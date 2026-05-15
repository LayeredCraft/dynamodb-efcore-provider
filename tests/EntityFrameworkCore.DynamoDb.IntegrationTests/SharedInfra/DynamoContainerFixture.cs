using Amazon.DynamoDBv2;
using Amazon.Runtime;
using JetBrains.Annotations;
using Testcontainers.DynamoDb;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

[UsedImplicitly]
public sealed class DynamoContainerFixture(IMessageSink messageSink)
    : ContainerFixture<DynamoDbBuilder, DynamoDbContainer>(messageSink)
{
    public IReadOnlyDictionary<Type, Type> MapperTypes { get; } = DiscoverMapperTypes();

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

    protected override DynamoDbBuilder Configure() => new("amazon/dynamodb-local:latest");

    private static IReadOnlyDictionary<Type, Type> DiscoverMapperTypes()
    {
        var mapperInterfaceType = typeof(IDynamoMapper<>);
        var discovered =
            typeof(DynamoContainerFixture)
                .Assembly
                .GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false })
                .SelectMany(type
                    => type
                        .GetInterfaces()
                        .Where(@interface => @interface.IsGenericType
                            && @interface.GetGenericTypeDefinition() == mapperInterfaceType)
                        .Select(@interface => new
                        {
                            ModelType = @interface.GetGenericArguments()[0], MapperType = type,
                        }))
                .ToArray();

        var duplicates = discovered
            .GroupBy(mapper => mapper.ModelType)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.FullName ?? group.Key.Name)
            .ToArray();

        if (duplicates.Length > 0)
            throw new InvalidOperationException(
                $"Multiple DynamoMapper classes found for model type(s): {string.Join(", ", duplicates)}.");

        var discoveredDict =
            discovered.ToDictionary(mapper => mapper.ModelType, mapper => mapper.MapperType);

        return discoveredDict;
    }
}
