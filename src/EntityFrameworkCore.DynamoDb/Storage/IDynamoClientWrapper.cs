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

    /// <summary>Executes a write PartiQL statement (INSERT, UPDATE, DELETE) and discards any result items.</summary>
    /// <param name="statement">The PartiQL write statement to execute.</param>
    /// <param name="parameters">Positional parameter values for the statement.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task ExecuteWriteAsync(
        string statement,
        List<AttributeValue> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>Executes an atomic write transaction composed of PartiQL statements.</summary>
    /// <param name="statements">Ordered transactional statements to execute atomically.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task ExecuteTransactionAsync(
        IReadOnlyList<ParameterizedStatement> statements,
        CancellationToken cancellationToken = default);
}
