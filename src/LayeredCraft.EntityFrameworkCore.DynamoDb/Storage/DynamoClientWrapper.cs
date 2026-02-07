using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Diagnostics.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;

public class DynamoClientWrapper : IDynamoClientWrapper
{
    private readonly AmazonDynamoDBConfig _amazonDynamoDbConfig = new();
    private readonly IExecutionStrategy _executionStrategy;
    private readonly IDiagnosticsLogger<DbLoggerCategory.Database.Command> _commandLogger;

    public DynamoClientWrapper(
        IDbContextOptions dbContextOptions,
        IExecutionStrategy executionStrategy,
        IDiagnosticsLogger<DbLoggerCategory.Database.Command> commandLogger)
    {
        var options = dbContextOptions.NotNull().FindExtension<DynamoDbOptionsExtension>();

        if (options?.AuthenticationRegion is not null)
            _amazonDynamoDbConfig.AuthenticationRegion = options.AuthenticationRegion;

        if (options?.ServiceUrl is not null)
            _amazonDynamoDbConfig.ServiceURL = options.ServiceUrl;

        _executionStrategy = executionStrategy.NotNull();
        _commandLogger = commandLogger.NotNull();
    }

    public virtual IAmazonDynamoDB Client
    {
        get
        {
            field ??= new AmazonDynamoDBClient(_amazonDynamoDbConfig);
            return field;
        }
    }

    public IAsyncEnumerable<Dictionary<string, AttributeValue>> ExecutePartiQl(
        ExecuteStatementRequest statementRequest,
        bool singlePageOnly = false)
        => new DynamoAsyncEnumerable(this, statementRequest, singlePageOnly);

    private sealed class DynamoAsyncEnumerable(
        DynamoClientWrapper dynamoClientWrapper,
        ExecuteStatementRequest statementRequest,
        bool singlePageOnly) : IAsyncEnumerable<Dictionary<string, AttributeValue>>
    {
        private readonly DynamoClientWrapper _dynamoClientWrapper = dynamoClientWrapper;
        private readonly ExecuteStatementRequest _statementRequestPrototype = statementRequest;
        private readonly bool _singlePageOnly = singlePageOnly;

        public IAsyncEnumerator<Dictionary<string, AttributeValue>> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
            => new AsyncEnumerator(this, cancellationToken);

        private sealed class AsyncEnumerator(
            DynamoAsyncEnumerable dynamoEnumerable,
            CancellationToken cancellationToken)
            : IAsyncEnumerator<Dictionary<string, AttributeValue>>
        {
            private readonly bool _singlePageOnly = dynamoEnumerable._singlePageOnly;

            private readonly ExecuteStatementRequest _request = CloneExecuteStatementRequest(
                dynamoEnumerable._statementRequestPrototype,
                true);

            private bool _hasExecutedRequest;
            private List<Dictionary<string, AttributeValue>>? _currentItems;
            private int _currentIndex = -1;
            private string? _nextToken = dynamoEnumerable._statementRequestPrototype.NextToken;
            private bool _hasMorePages = true;

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
                        ._dynamoClientWrapper._executionStrategy.ExecuteAsync(
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

            private async Task<bool> FetchPageAsync(CancellationToken ct)
            {
                _request.NextToken = _nextToken;

                dynamoEnumerable._dynamoClientWrapper._commandLogger.ExecutingExecuteStatement(
                    _request.Limit,
                    _request.NextToken is not null);

                var response =
                    await dynamoEnumerable
                        ._dynamoClientWrapper.Client.ExecuteStatementAsync(_request, ct)
                        .ConfigureAwait(false);

                dynamoEnumerable._dynamoClientWrapper._commandLogger.ExecutedExecuteStatement(
                    response.Items?.Count ?? 0,
                    response.NextToken is not null);

                _hasExecutedRequest = true;
                _currentItems = response.Items;
                _nextToken = response.NextToken;
                _hasMorePages = !string.IsNullOrEmpty(_nextToken);

                return true;
            }

            public ValueTask DisposeAsync()
            {
                _currentItems = null;
                return default;
            }
        }
    }

    private static ExecuteStatementRequest
        CloneExecuteStatementRequest(ExecuteStatementRequest prototype, bool cloneParameters)
        => new()
        {
            Statement = prototype.Statement,
            Parameters =
                cloneParameters && prototype.Parameters is not null
                    ? new List<AttributeValue>(prototype.Parameters)
                    : prototype.Parameters,
            Limit = prototype.Limit,
            ConsistentRead = prototype.ConsistentRead,
            ReturnConsumedCapacity = prototype.ReturnConsumedCapacity,
            ReturnValuesOnConditionCheckFailure = prototype.ReturnValuesOnConditionCheckFailure,
        };
}
