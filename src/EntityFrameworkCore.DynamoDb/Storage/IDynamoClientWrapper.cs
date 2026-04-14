using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>Defines the contract for IDynamoClientWrapper.</summary>
public interface IDynamoClientWrapper
{
    /// <summary>Provides functionality for this member.</summary>
    IAmazonDynamoDB Client { get; }

    /// <summary>Executes a PartiQL statement and streams projected item dictionaries page by page.</summary>
    /// <param name="statementRequest">The PartiQL statement and execution parameters.</param>
    /// <param name="singlePageOnly">
    ///     When <see langword="true" />, stops after the first page regardless of
    ///     pagination tokens.
    /// </param>
    /// <param name="onPageFetched">
    ///     Optional callback invoked with the raw
    ///     <see cref="ExecuteStatementResponse" /> immediately after each page is fetched and before its
    ///     items are yielded.
    /// </param>
    IAsyncEnumerable<Dictionary<string, AttributeValue>> ExecutePartiQl(
        ExecuteStatementRequest statementRequest,
        bool singlePageOnly = false,
        Action<ExecuteStatementResponse>? onPageFetched = null);

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

    /// <summary>Executes non-atomic PartiQL batch write statements.</summary>
    /// <param name="statements">Ordered batch statements to execute.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>Per-statement responses for the submitted batch.</returns>
    Task<IReadOnlyList<BatchStatementResponse>> ExecuteBatchWriteAsync(
        IReadOnlyList<BatchStatementRequest> statements,
        CancellationToken cancellationToken = default);
}
