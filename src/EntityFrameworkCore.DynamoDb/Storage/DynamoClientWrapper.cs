using System.Diagnostics;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Diagnostics.Internal;
using EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>Represents the DynamoClientWrapper type.</summary>
public class DynamoClientWrapper : IDynamoClientWrapper
{
    private readonly AmazonDynamoDBConfig? _amazonDynamoDbConfig;
    private readonly ReturnConsumedCapacity? _returnConsumedCapacity;
    private readonly bool _consistentRead;
    private readonly IDiagnosticsLogger<DbLoggerCategory.Database.Command> _commandLogger;
    private readonly IExecutionStrategy _executionStrategy;

    /// <summary>Creates a client wrapper using provider options and EF Core execution services.</summary>
    public DynamoClientWrapper(
        IDbContextOptions dbContextOptions,
        IExecutionStrategy executionStrategy,
        IDiagnosticsLogger<DbLoggerCategory.Database.Command> commandLogger)
    {
        var options =
            dbContextOptions.NotNull().FindExtension<DynamoDbOptionsExtension>().NotNull();

        if (options.DynamoDbClient is not null)
            Client = options.DynamoDbClient;
        else
            _amazonDynamoDbConfig = BuildAmazonDynamoDbConfig(options);

        _returnConsumedCapacity = options.ReturnConsumedCapacity;
        _consistentRead = options.ConsistentRead;
        _executionStrategy = executionStrategy.NotNull();
        _commandLogger = commandLogger.NotNull();
    }

    /// <summary>Gets the resolved DynamoDB client, preferring an explicitly configured client instance.</summary>
    public virtual IAmazonDynamoDB Client
    {
        get
        {
            field ??= new AmazonDynamoDBClient(_amazonDynamoDbConfig.NotNull());
            return field;
        }
    }

    /// <summary>Creates a reusable async enumerable over PartiQL result pages.</summary>
    public IAsyncEnumerable<Dictionary<string, AttributeValue>> ExecutePartiQl(
        ExecuteStatementRequest statementRequest,
        bool singlePageOnly = false,
        Action<ExecuteStatementResponse>? onPageFetched = null,
        bool suppressConsistentReadDefault = false)
    {
        var request = CloneExecuteStatementRequest(statementRequest, false);
        request.ReturnConsumedCapacity ??= _returnConsumedCapacity;
        if (!suppressConsistentReadDefault)
            request.ConsistentRead ??= _consistentRead;

        return new DynamoAsyncEnumerable(this, request, singlePageOnly, onPageFetched);
    }

    /// <summary>Executes a write PartiQL statement (INSERT, UPDATE, DELETE) and discards any result items.</summary>
    /// <param name="statement">The PartiQL write statement to execute.</param>
    /// <param name="parameters">Positional parameter values for the statement.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    public Task ExecuteWriteAsync(
        string statement,
        List<AttributeValue> parameters,
        CancellationToken cancellationToken = default)
        => _executionStrategy.ExecuteAsync(
            (statement, parameters),
            async (_, state, ct) =>
            {
                var request = new ExecuteStatementRequest
                {
                    Statement = state.statement,
                    Parameters = state.parameters?.Count > 0 ? state.parameters : null,
                    ReturnValuesOnConditionCheckFailure =
                        ReturnValuesOnConditionCheckFailure.ALL_OLD,
                    ReturnConsumedCapacity = _returnConsumedCapacity,
                };

                var commandId = Guid.NewGuid();
                var stopwatch = Stopwatch.StartNew();
                _commandLogger.ExecutingPartiQlWriteRequest(
                    DynamoPartiQlWriteOperation.ExecuteStatement,
                    1,
                    commandId);

                ExecuteStatementResponse response;
                try
                {
                    response =
                        await Client.ExecuteStatementAsync(request, ct).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    stopwatch.Stop();
                    _commandLogger.PartiQlWriteRequestFailed(
                        DynamoPartiQlWriteOperation.ExecuteStatement,
                        1,
                        exception,
                        stopwatch.Elapsed,
                        commandId,
                        (exception as AmazonServiceException)?.RequestId);
                    throw;
                }

                stopwatch.Stop();
                _commandLogger.ExecutedPartiQlWriteRequest(
                    DynamoPartiQlWriteOperation.ExecuteStatement,
                    1,
                    stopwatch.Elapsed,
                    commandId,
                    response.ResponseMetadata?.RequestId,
                    response.ConsumedCapacity is null ? null : [response.ConsumedCapacity]);

                return true;
            },
            null,
            cancellationToken);

    /// <summary>Executes an atomic write transaction of PartiQL statements.</summary>
    /// <param name="statements">Ordered transaction statements.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    public Task ExecuteTransactionAsync(
        IReadOnlyList<ParameterizedStatement> statements,
        CancellationToken cancellationToken = default)
        => _executionStrategy.ExecuteAsync(
            statements,
            async (_, transactionStatements, ct) =>
            {
                var request = new ExecuteTransactionRequest
                {
                    TransactStatements = [.. transactionStatements],
                    ReturnConsumedCapacity = _returnConsumedCapacity,
                };

                var commandId = Guid.NewGuid();
                var stopwatch = Stopwatch.StartNew();
                var statementCount = request.TransactStatements?.Count ?? 0;
                _commandLogger.ExecutingPartiQlWriteRequest(
                    DynamoPartiQlWriteOperation.ExecuteTransaction,
                    statementCount,
                    commandId);

                ExecuteTransactionResponse response;
                try
                {
                    response =
                        await Client.ExecuteTransactionAsync(request, ct).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    stopwatch.Stop();
                    _commandLogger.PartiQlWriteRequestFailed(
                        DynamoPartiQlWriteOperation.ExecuteTransaction,
                        statementCount,
                        exception,
                        stopwatch.Elapsed,
                        commandId,
                        (exception as AmazonServiceException)?.RequestId);
                    throw;
                }

                stopwatch.Stop();
                _commandLogger.ExecutedPartiQlWriteRequest(
                    DynamoPartiQlWriteOperation.ExecuteTransaction,
                    statementCount,
                    stopwatch.Elapsed,
                    commandId,
                    response.ResponseMetadata?.RequestId,
                    response.ConsumedCapacity);

                return true;
            },
            null,
            cancellationToken);

    /// <summary>Executes non-atomic PartiQL batch write statements.</summary>
    /// <param name="statements">Ordered batch statements.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>Per-statement responses returned by DynamoDB.</returns>
    public Task<IReadOnlyList<BatchStatementResponse>> ExecuteBatchWriteAsync(
        IReadOnlyList<BatchStatementRequest> statements,
        CancellationToken cancellationToken = default)
        => _executionStrategy.ExecuteAsync(
            statements,
            async (_, batchStatements, ct) =>
            {
                var request = new BatchExecuteStatementRequest
                {
                    Statements = [.. batchStatements],
                    ReturnConsumedCapacity = _returnConsumedCapacity,
                };

                var commandId = Guid.NewGuid();
                var stopwatch = Stopwatch.StartNew();
                var statementCount = request.Statements?.Count ?? 0;
                _commandLogger.ExecutingPartiQlWriteRequest(
                    DynamoPartiQlWriteOperation.BatchExecuteStatement,
                    statementCount,
                    commandId);

                BatchExecuteStatementResponse response;
                try
                {
                    response =
                        await Client.BatchExecuteStatementAsync(request, ct).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    stopwatch.Stop();
                    _commandLogger.PartiQlWriteRequestFailed(
                        DynamoPartiQlWriteOperation.BatchExecuteStatement,
                        statementCount,
                        exception,
                        stopwatch.Elapsed,
                        commandId,
                        (exception as AmazonServiceException)?.RequestId);
                    throw;
                }

                stopwatch.Stop();
                _commandLogger.ExecutedPartiQlWriteRequest(
                    DynamoPartiQlWriteOperation.BatchExecuteStatement,
                    statementCount,
                    stopwatch.Elapsed,
                    commandId,
                    response.ResponseMetadata?.RequestId,
                    response.ConsumedCapacity);

                var responses = (IReadOnlyList<BatchStatementResponse>)(response.Responses ?? []);
                var errorCount = responses.Count(r => r.Error is not null);
                if (errorCount > 0)
                    _commandLogger.BatchPartiQlWriteReturnedStatementErrors(
                        statementCount,
                        errorCount,
                        commandId,
                        response.ResponseMetadata?.RequestId);

                return responses;
            },
            null,
            cancellationToken);

    /// <summary>Builds the effective SDK configuration from extension options in precedence order.</summary>
    private static AmazonDynamoDBConfig BuildAmazonDynamoDbConfig(DynamoDbOptionsExtension? options)
    {
        if (options?.DynamoDbClientConfig is not null)
            return options.DynamoDbClientConfig;

        var config = new AmazonDynamoDBConfig();
        options?.DynamoDbClientConfigAction?.Invoke(config);

        return config;
    }

    /// <summary>Clones a statement request so enumeration can mutate paging state safely.</summary>
    private static ExecuteStatementRequest
        CloneExecuteStatementRequest(ExecuteStatementRequest prototype, bool cloneParameters)
        => new()
        {
            Statement = prototype.Statement,
            Parameters =
                cloneParameters && prototype.Parameters is not null
                    ? [..prototype.Parameters]
                    : prototype.Parameters,
            Limit = prototype.Limit,
            NextToken = prototype.NextToken,
            ConsistentRead = prototype.ConsistentRead,
            ReturnConsumedCapacity = prototype.ReturnConsumedCapacity,
            ReturnValuesOnConditionCheckFailure = prototype.ReturnValuesOnConditionCheckFailure,
        };

    private sealed class DynamoAsyncEnumerable(
        DynamoClientWrapper dynamoClientWrapper,
        ExecuteStatementRequest statementRequest,
        bool singlePageOnly,
        Action<ExecuteStatementResponse>? onPageFetched)
        : IAsyncEnumerable<Dictionary<string, AttributeValue>>
    {
        private readonly DynamoClientWrapper _dynamoClientWrapper = dynamoClientWrapper;
        private readonly bool _singlePageOnly = singlePageOnly;
        private readonly ExecuteStatementRequest _statementRequestPrototype = statementRequest;

        /// <summary>
        ///     Invoked with the raw SDK response immediately after each page is fetched, before items
        ///     from that page are yielded. Used to propagate per-page response metadata.
        /// </summary>
        private readonly Action<ExecuteStatementResponse>? _onPageFetched = onPageFetched;

        /// <summary>Provides functionality for this member.</summary>
        public IAsyncEnumerator<Dictionary<string, AttributeValue>> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
            => new AsyncEnumerator(this, cancellationToken);

        private sealed class AsyncEnumerator(
            DynamoAsyncEnumerable dynamoEnumerable,
            CancellationToken cancellationToken)
            : IAsyncEnumerator<Dictionary<string, AttributeValue>>
        {
            private readonly ExecuteStatementRequest _request = CloneExecuteStatementRequest(
                dynamoEnumerable._statementRequestPrototype,
                true);

            private readonly bool _singlePageOnly = dynamoEnumerable._singlePageOnly;
            private int _currentIndex = -1;
            private List<Dictionary<string, AttributeValue>>? _currentItems;

            private bool _hasExecutedRequest;
            private bool _hasMorePages = true;
            private string? _nextToken = dynamoEnumerable._statementRequestPrototype.NextToken;

            /// <summary>Provides functionality for this member.</summary>
            public Dictionary<string, AttributeValue> Current
            {
                get
                {
                    if (_currentItems is null
                        || _currentIndex < 0
                        || _currentIndex >= _currentItems.Count)
                        throw new InvalidOperationException(
                            "Enumeration has not started or has already finished.");

                    return _currentItems[_currentIndex];
                }
            }

            /// <summary>Provides functionality for this member.</summary>
            public async ValueTask<bool> MoveNextAsync()
            {
                while (true)
                {
                    // If we have items in the current batch, try to move to the next one
                    if (_currentItems is not null && _currentIndex + 1 < _currentItems.Count)
                    {
                        _currentIndex++;
                        return true;
                    }

                    // If single page mode and we've already executed, stop
                    if (_singlePageOnly && _hasExecutedRequest)
                        return false;

                    // If we don't have more pages, we're done
                    if (!_hasMorePages)
                        return false;

                    // Fetch the next page
                    await dynamoEnumerable
                        ._dynamoClientWrapper
                        ._executionStrategy
                        .ExecuteAsync(
                            this,
                            static (_, enumerator, ct) => enumerator.FetchPageAsync(ct),
                            null,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (_currentItems is not null && _currentItems.Count > 0)
                    {
                        _currentIndex = 0;
                        return true;
                    }

                    if (!_hasMorePages)
                        return false;
                }
            }

            /// <summary>Provides functionality for this member.</summary>
            public ValueTask DisposeAsync()
            {
                _currentItems = null;
                return default;
            }

            private async Task<bool> FetchPageAsync(CancellationToken ct)
            {
                _request.NextToken = _nextToken;

                var isFirstRequest = !_hasExecutedRequest;
                var seedNextTokenPresent = isFirstRequest && _request.NextToken is not null;

                var commandId = Guid.NewGuid();
                var stopwatch = Stopwatch.StartNew();

                dynamoEnumerable._dynamoClientWrapper._commandLogger.ExecutingExecuteStatement(
                    _request.Limit,
                    _request.NextToken is not null,
                    seedNextTokenPresent,
                    commandId);

                ExecuteStatementResponse response;
                try
                {
                    response = await dynamoEnumerable
                        ._dynamoClientWrapper
                        .Client
                        .ExecuteStatementAsync(_request, ct)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    stopwatch.Stop();
                    dynamoEnumerable._dynamoClientWrapper._commandLogger.ExecuteStatementFailed(
                        exception,
                        stopwatch.Elapsed,
                        commandId,
                        (exception as AmazonServiceException)?.RequestId,
                        _request.Limit,
                        _request.NextToken is not null,
                        seedNextTokenPresent);
                    throw;
                }

                stopwatch.Stop();

                dynamoEnumerable._dynamoClientWrapper._commandLogger.ExecutedExecuteStatement(
                    response.Items?.Count ?? 0,
                    response.NextToken is not null,
                    stopwatch.Elapsed,
                    commandId,
                    response.ResponseMetadata?.RequestId,
                    _request.Limit,
                    seedNextTokenPresent,
                    response.ConsumedCapacity);

                // Notify before items are yielded so callers can capture per-page metadata.
                dynamoEnumerable._onPageFetched?.Invoke(response);

                _hasExecutedRequest = true;
                _currentItems = response.Items;
                _nextToken = response.NextToken;
                _hasMorePages = !string.IsNullOrEmpty(_nextToken);

                return true;
            }
        }
    }
}
