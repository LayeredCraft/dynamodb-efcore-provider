using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;

public class DynamoClientWrapper : IDynamoClientWrapper
{
    private readonly AmazonDynamoDBConfig _amazonDynamoDbConfig = new();

    public DynamoClientWrapper(IDbContextOptions dbContextOptions)
    {
        var options = dbContextOptions.NotNull().FindExtension<DynamoDbOptionsExtension>();

        if (options?.AuthenticationRegion is not null)
            _amazonDynamoDbConfig.AuthenticationRegion = options.AuthenticationRegion;

        if (options?.ServiceUrl is not null)
            _amazonDynamoDbConfig.ServiceURL = options.ServiceUrl;
    }

    public IAmazonDynamoDB Client
    {
        get
        {
            field ??= new AmazonDynamoDBClient(_amazonDynamoDbConfig);
            return field;
        }
    }

    public async Task<List<Dictionary<string, AttributeValue>>> ExecutePartiQl<T>(
        PartiQlQuery query)
    {
        var request = new ExecuteStatementRequest
        {
            Statement = query.Statement, Parameters = query.Parameters,
        };

        var response = await Client.ExecuteStatementAsync(request);

        return response.Items;
    }
}
