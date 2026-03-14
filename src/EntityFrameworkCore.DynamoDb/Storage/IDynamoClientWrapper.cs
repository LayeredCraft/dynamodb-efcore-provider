using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>Defines the contract for IDynamoClientWrapper.</summary>
public interface IDynamoClientWrapper
{
    /// <summary>Provides functionality for this member.</summary>
    IAmazonDynamoDB Client { get; }

    /// <summary>Executes a PartiQL statement and streams projected item dictionaries.</summary>
    IAsyncEnumerable<Dictionary<string, AttributeValue>> ExecutePartiQl(
        ExecuteStatementRequest statementRequest,
        bool singlePageOnly = false);
}
