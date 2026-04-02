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

    /// <summary>
    ///     Executes a single PartiQL write statement (INSERT, UPDATE, or DELETE) and returns
    ///     the AWS SDK response.
    /// </summary>
    /// <param name="statementRequest">The PartiQL write statement with positional parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="ExecuteStatementResponse" /> from the DynamoDB service.</returns>
    Task<ExecuteStatementResponse> ExecuteWriteStatementAsync(
        ExecuteStatementRequest statementRequest,
        CancellationToken cancellationToken = default);
}
