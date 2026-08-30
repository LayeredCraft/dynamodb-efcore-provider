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
using DynamoDbLoggerCategory = Microsoft.EntityFrameworkCore.DynamoDB.DbLoggerCategory;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>Represents the DynamoClientWrapper type.</summary>
public class DynamoClientWrapper : IDynamoClientWrapper, IDisposable
{
    private readonly AmazonDynamoDBConfig? _amazonDynamoDbConfig;
    private readonly ReturnConsumedCapacity? _returnConsumedCapacity;
    private readonly bool _consistentRead;
    private readonly IDiagnosticsLogger<DbLoggerCategory.Database.Command> _commandLogger;
    private readonly IDiagnosticsLogger<DynamoDbLoggerCategory.Capacity> _capacityLogger;
    private readonly IExecutionStrategy _executionStrategy;
    private readonly IDynamoDbCommandInterceptor? _commandInterceptor;
    private readonly DbContext? _context;
    private bool _ownsClient;
    private bool _disposed;

    /// <summary>Creates a client wrapper using provider options and EF Core execution services.</summary>
    public DynamoClientWrapper(
        IDbContextOptions dbContextOptions,
        IExecutionStrategy executionStrategy,
        IDiagnosticsLogger<DbLoggerCategory.Database.Command> commandLogger,
        IDiagnosticsLogger<DynamoDbLoggerCategory.Capacity> capacityLogger,
        IInterceptors? interceptors = null,
        ICurrentDbContext? currentDbContext = null)
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
        _capacityLogger = capacityLogger.NotNull();
        _commandInterceptor = interceptors?.Aggregate<IDynamoDbCommandInterceptor>();
        _context = currentDbContext?.Context;
    }

    /// <summary>Gets the resolved DynamoDB client, preferring an explicitly configured client instance.</summary>
    public virtual IAmazonDynamoDB Client
    {
        get
        {
            if (field is null)
            {
                field = new AmazonDynamoDBClient(_amazonDynamoDbConfig.NotNull());
                _ownsClient = true;
            }

            return field;
        }
    }

    /// <summary>Disposes the provider-created DynamoDB client, if this wrapper owns it.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_ownsClient)
            Client.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>Creates a reusable async enumerable over PartiQL result pages.</summary>
    public IAsyncEnumerable<Dictionary<string, AttributeValue>> ExecutePartiQl(
        ExecuteStatementRequest statementRequest,
        bool singlePageOnly = false,
        Action<ExecuteStatementResponse>? onPageFetched = null,
        bool suppressConsistentReadDefault = false)
    {
        DynamoPartiQlStatementValidator.ValidateStatementLength(statementRequest.Statement, "read");

        var request = CloneExecuteStatementRequest(statementRequest, true);
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
    {
        var attempt = new ExecutionAttempt();
        return _executionStrategy.ExecuteAsync(
            (statement, parameters, attempt),
            async (_, state, ct) =>
            {
                var request = new ExecuteStatementRequest
                {
                    Statement = state.statement,
                    Parameters = state.parameters?.Count > 0 ? state.parameters : null,
                    ReturnValuesOnConditionCheckFailure =
                        ReturnValuesOnConditionCheckFailure.ALL_OLD,
                    ReturnConsumedCapacity = _returnConsumedCapacity
                };

                var commandId = Guid.NewGuid();
                _commandLogger.ExecutingPartiQlWriteRequest(
                    DynamoPartiQlWriteOperation.ExecuteStatement,
                    1,
                    commandId);

                await ExecuteSdkCallAsync(
                        request,
                        DynamoDbCommandOperation.ExecuteStatementWrite,
                        commandId,
                        state.attempt.Next(),
                        null,
                        token => Client.ExecuteStatementAsync(request, token),
                        (response, elapsed) =>
                        {
                            _commandLogger.ExecutedPartiQlWriteRequest(
                                DynamoPartiQlWriteOperation.ExecuteStatement,
                                1,
                                elapsed,
                                commandId,
                                response.ResponseMetadata?.RequestId,
                                response.ConsumedCapacity is null
                                    ? null
                                    : [response.ConsumedCapacity]);
                            _capacityLogger.ConsumedCapacity(
                                commandId,
                                response.ConsumedCapacity is null
                                    ? null
                                    : [response.ConsumedCapacity]);
                        },
                        (exception, elapsed) =>
                        {
                            _commandLogger.PartiQlWriteRequestFailed(
                                DynamoPartiQlWriteOperation.ExecuteStatement,
                                1,
                                exception,
                                elapsed,
                                commandId,
                                (exception as AmazonServiceException)?.RequestId);
                        },
                        response => response.ConsumedCapacity is null
                            ? null
                            : [response.ConsumedCapacity],
                        ct)
                    .ConfigureAwait(false);

                return true;
            },
            null,
            cancellationToken);
    }

    /// <summary>Executes an atomic write transaction of PartiQL statements.</summary>
    /// <param name="statements">Ordered transaction statements.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    public Task ExecuteTransactionAsync(
        IReadOnlyList<ParameterizedStatement> statements,
        CancellationToken cancellationToken = default)
    {
        var attempt = new ExecutionAttempt();
        return _executionStrategy.ExecuteAsync(
            (statements, attempt),
            async (_, transactionStatements, ct) =>
            {
                var request = new ExecuteTransactionRequest
                {
                    TransactStatements = [.. transactionStatements.statements],
                    ReturnConsumedCapacity = _returnConsumedCapacity
                };

                var commandId = Guid.NewGuid();
                var statementCount = request.TransactStatements?.Count ?? 0;
                _commandLogger.ExecutingPartiQlWriteRequest(
                    DynamoPartiQlWriteOperation.ExecuteTransaction,
                    statementCount,
                    commandId);

                await ExecuteSdkCallAsync(
                        request,
                        DynamoDbCommandOperation.ExecuteTransaction,
                        commandId,
                        transactionStatements.attempt.Next(),
                        null,
                        token => Client.ExecuteTransactionAsync(request, token),
                        (response, elapsed) =>
                        {
                            _commandLogger.ExecutedPartiQlWriteRequest(
                                DynamoPartiQlWriteOperation.ExecuteTransaction,
                                statementCount,
                                elapsed,
                                commandId,
                                response.ResponseMetadata?.RequestId,
                                response.ConsumedCapacity);
                            _capacityLogger.ConsumedCapacity(commandId, response.ConsumedCapacity);
                        },
                        (exception, elapsed) =>
                        {
                            _commandLogger.PartiQlWriteRequestFailed(
                                DynamoPartiQlWriteOperation.ExecuteTransaction,
                                statementCount,
                                exception,
                                elapsed,
                                commandId,
                                (exception as AmazonServiceException)?.RequestId);
                        },
                        response => response.ConsumedCapacity,
                        ct)
                    .ConfigureAwait(false);

                return true;
            },
            null,
            cancellationToken);
    }

    /// <summary>Executes non-atomic PartiQL batch write statements.</summary>
    /// <param name="statements">Ordered batch statements.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>Per-statement responses returned by DynamoDB.</returns>
    public Task<IReadOnlyList<BatchStatementResponse>> ExecuteBatchWriteAsync(
        IReadOnlyList<BatchStatementRequest> statements,
        CancellationToken cancellationToken = default)
    {
        var attempt = new ExecutionAttempt();
        return _executionStrategy.ExecuteAsync(
            (statements, attempt),
            async (_, batchStatements, ct) =>
            {
                var request = new BatchExecuteStatementRequest
                {
                    Statements = [.. batchStatements.statements],
                    ReturnConsumedCapacity = _returnConsumedCapacity
                };

                var commandId = Guid.NewGuid();
                var statementCount = request.Statements?.Count ?? 0;
                _commandLogger.ExecutingPartiQlWriteRequest(
                    DynamoPartiQlWriteOperation.BatchExecuteStatement,
                    statementCount,
                    commandId);

                var response = await ExecuteSdkCallAsync(
                        request,
                        DynamoDbCommandOperation.BatchExecuteStatement,
                        commandId,
                        batchStatements.attempt.Next(),
                        null,
                        token => Client.BatchExecuteStatementAsync(request, token),
                        (response, elapsed) =>
                        {
                            _commandLogger.ExecutedPartiQlWriteRequest(
                                DynamoPartiQlWriteOperation.BatchExecuteStatement,
                                statementCount,
                                elapsed,
                                commandId,
                                response.ResponseMetadata?.RequestId,
                                response.ConsumedCapacity);
                            _capacityLogger.ConsumedCapacity(commandId, response.ConsumedCapacity);
                        },
                        (exception, elapsed) =>
                        {
                            _commandLogger.PartiQlWriteRequestFailed(
                                DynamoPartiQlWriteOperation.BatchExecuteStatement,
                                statementCount,
                                exception,
                                elapsed,
                                commandId,
                                (exception as AmazonServiceException)?.RequestId);
                        },
                        response => response.ConsumedCapacity,
                        ct)
                    .ConfigureAwait(false);

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
    }

    private async Task<TResponse> ExecuteSdkCallAsync<TRequest, TResponse>(
        TRequest request,
        DynamoDbCommandOperation operation,
        Guid commandId,
        int attemptNumber,
        int? pageNumber,
        Func<CancellationToken, Task<TResponse>> execute,
        Action<TResponse, TimeSpan> executed,
        Action<Exception, TimeSpan> failed,
        Func<TResponse, IReadOnlyList<ConsumedCapacity>?> consumedCapacities,
        CancellationToken cancellationToken)
        where TRequest : AmazonWebServiceRequest where TResponse : AmazonWebServiceResponse
    {
        if (_commandInterceptor is null)
            return await ExecuteWithoutInterceptionAsync().ConfigureAwait(false);

        var eventData = new DynamoDbCommandEventData(
            _context,
            request,
            operation,
            commandId,
            attemptNumber,
            pageNumber);
        await CommandExecutingAsync(_commandInterceptor, request, eventData, cancellationToken)
            .ConfigureAwait(false);

        return await ExecuteWithInterceptionAsync().ConfigureAwait(false);

        async Task<TResponse> ExecuteWithoutInterceptionAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await execute(cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();
                executed(response, stopwatch.Elapsed);
                return response;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                throw;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                failed(exception, stopwatch.Elapsed);
                throw;
            }
        }

        async Task<TResponse> ExecuteWithInterceptionAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            TResponse response;
            try
            {
                response = await execute(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                await CommandCanceledAsync(
                        _commandInterceptor,
                        request,
                        new DynamoDbCommandEndEventData(
                            _context,
                            request,
                            operation,
                            commandId,
                            attemptNumber,
                            pageNumber,
                            stopwatch.Elapsed,
                            null),
                        cancellationToken)
                    .ConfigureAwait(false);
                throw;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                failed(exception, stopwatch.Elapsed);
                await CommandFailedAsync(
                        _commandInterceptor,
                        request,
                        new DynamoDbCommandErrorEventData(
                            _context,
                            request,
                            operation,
                            commandId,
                            attemptNumber,
                            pageNumber,
                            stopwatch.Elapsed,
                            (exception as AmazonServiceException)?.RequestId,
                            exception),
                        cancellationToken)
                    .ConfigureAwait(false);
                throw;
            }

            stopwatch.Stop();
            executed(response, stopwatch.Elapsed);
            await CommandExecutedAsync(
                    _commandInterceptor,
                    request,
                    new DynamoDbCommandExecutedEventData(
                        _context,
                        request,
                        operation,
                        commandId,
                        attemptNumber,
                        pageNumber,
                        stopwatch.Elapsed,
                        response.ResponseMetadata?.RequestId,
                        response,
                        consumedCapacities(response)),
                    response,
                    cancellationToken)
                .ConfigureAwait(false);
            return response;
        }
    }

    private static ValueTask CommandExecutingAsync(
        IDynamoDbCommandInterceptor interceptor,
        AmazonWebServiceRequest request,
        DynamoDbCommandEventData eventData,
        CancellationToken cancellationToken)
        => request switch
        {
            ExecuteStatementRequest executeStatement => interceptor.ExecuteStatementExecutingAsync(
                executeStatement,
                eventData,
                cancellationToken),
            ExecuteTransactionRequest executeTransaction => interceptor
                .ExecuteTransactionExecutingAsync(executeTransaction, eventData, cancellationToken),
            BatchExecuteStatementRequest batchExecuteStatement => interceptor
                .BatchExecuteStatementExecutingAsync(
                    batchExecuteStatement,
                    eventData,
                    cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unsupported DynamoDB request type '{request.GetType().Name}'.")
        };

    private static ValueTask CommandExecutedAsync<TResponse>(
        IDynamoDbCommandInterceptor interceptor,
        AmazonWebServiceRequest request,
        DynamoDbCommandExecutedEventData eventData,
        TResponse response,
        CancellationToken cancellationToken) where TResponse : AmazonWebServiceResponse
        => request switch
        {
            ExecuteStatementRequest executeStatement => interceptor.ExecuteStatementExecutedAsync(
                executeStatement,
                eventData,
                (ExecuteStatementResponse)(object)response,
                cancellationToken),
            ExecuteTransactionRequest executeTransaction =>
                interceptor.ExecuteTransactionExecutedAsync(
                    executeTransaction,
                    eventData,
                    (ExecuteTransactionResponse)(object)response,
                    cancellationToken),
            BatchExecuteStatementRequest batchExecuteStatement =>
                interceptor.BatchExecuteStatementExecutedAsync(
                    batchExecuteStatement,
                    eventData,
                    (BatchExecuteStatementResponse)(object)response,
                    cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unsupported DynamoDB request type '{request.GetType().Name}'.")
        };

    private static Task CommandCanceledAsync(
        IDynamoDbCommandInterceptor interceptor,
        AmazonWebServiceRequest request,
        DynamoDbCommandEndEventData eventData,
        CancellationToken cancellationToken)
        => request switch
        {
            ExecuteStatementRequest executeStatement => interceptor.ExecuteStatementCanceledAsync(
                executeStatement,
                eventData,
                cancellationToken),
            ExecuteTransactionRequest executeTransaction => interceptor
                .ExecuteTransactionCanceledAsync(executeTransaction, eventData, cancellationToken),
            BatchExecuteStatementRequest batchExecuteStatement => interceptor
                .BatchExecuteStatementCanceledAsync(
                    batchExecuteStatement,
                    eventData,
                    cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unsupported DynamoDB request type '{request.GetType().Name}'.")
        };

    private static Task CommandFailedAsync(
        IDynamoDbCommandInterceptor interceptor,
        AmazonWebServiceRequest request,
        DynamoDbCommandErrorEventData eventData,
        CancellationToken cancellationToken)
        => request switch
        {
            ExecuteStatementRequest executeStatement => interceptor.ExecuteStatementFailedAsync(
                executeStatement,
                eventData,
                cancellationToken),
            ExecuteTransactionRequest executeTransaction => interceptor
                .ExecuteTransactionFailedAsync(executeTransaction, eventData, cancellationToken),
            BatchExecuteStatementRequest batchExecuteStatement => interceptor
                .BatchExecuteStatementFailedAsync(
                    batchExecuteStatement,
                    eventData,
                    cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unsupported DynamoDB request type '{request.GetType().Name}'.")
        };

    private sealed class ExecutionAttempt
    {
        private int _value;

        public int Next() => ++_value;
    }

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
                    ? [.. prototype.Parameters]
                    : prototype.Parameters,
            Limit = prototype.Limit,
            NextToken = prototype.NextToken,
            ConsistentRead = prototype.ConsistentRead,
            ReturnConsumedCapacity = prototype.ReturnConsumedCapacity,
            ReturnValuesOnConditionCheckFailure = prototype.ReturnValuesOnConditionCheckFailure
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
            private int _attemptNumber;
            private int _pageNumber;
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
                var pageNumber = _pageNumber + 1;
                var attemptNumber = ++_attemptNumber;

                dynamoEnumerable._dynamoClientWrapper._commandLogger.ExecutingExecuteStatement(
                    _request.Limit,
                    _request.NextToken is not null,
                    seedNextTokenPresent,
                    commandId);

                var response = await dynamoEnumerable
                    ._dynamoClientWrapper
                    .ExecuteSdkCallAsync(
                        _request,
                        DynamoDbCommandOperation.ExecuteStatementQuery,
                        commandId,
                        attemptNumber,
                        pageNumber,
                        token => dynamoEnumerable._dynamoClientWrapper.Client.ExecuteStatementAsync(
                            _request,
                            token),
                        (response, elapsed) =>
                        {
                            dynamoEnumerable._dynamoClientWrapper._commandLogger
                                .ExecutedExecuteStatement(
                                    response.Items?.Count ?? 0,
                                    response.NextToken is not null,
                                    elapsed,
                                    commandId,
                                    response.ResponseMetadata?.RequestId,
                                    _request.Limit,
                                    seedNextTokenPresent,
                                    response.ConsumedCapacity);
                            dynamoEnumerable._dynamoClientWrapper._capacityLogger.ConsumedCapacity(
                                commandId,
                                response.ConsumedCapacity is null
                                    ? null
                                    : [response.ConsumedCapacity]);
                        },
                        (exception, elapsed) =>
                        {
                            dynamoEnumerable._dynamoClientWrapper._commandLogger
                                .ExecuteStatementFailed(
                                    exception,
                                    elapsed,
                                    commandId,
                                    (exception as AmazonServiceException)?.RequestId,
                                    _request.Limit,
                                    _request.NextToken is not null,
                                    seedNextTokenPresent);
                        },
                        response => response.ConsumedCapacity is null
                            ? null
                            : [response.ConsumedCapacity],
                        ct)
                    .ConfigureAwait(false);

                // Notify before items are yielded so callers can capture per-page metadata.
                dynamoEnumerable._onPageFetched?.Invoke(response);

                _hasExecutedRequest = true;
                _attemptNumber = 0;
                _pageNumber++;
                _currentItems = response.Items;
                _nextToken = response.NextToken;
                _hasMorePages = !string.IsNullOrEmpty(_nextToken);

                return true;
            }
        }
    }
}
