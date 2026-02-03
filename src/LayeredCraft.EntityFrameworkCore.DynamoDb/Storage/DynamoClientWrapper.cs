using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;

public class DynamoClientWrapper : IDynamoClientWrapper
{
    private readonly AmazonDynamoDBConfig _amazonDynamoDbConfig = new();
    private readonly IExecutionStrategy _executionStrategy;

    public DynamoClientWrapper(
        IDbContextOptions dbContextOptions,
        IExecutionStrategy executionStrategy)
    {
        var options = dbContextOptions.NotNull().FindExtension<DynamoDbOptionsExtension>();

        if (options?.AuthenticationRegion is not null)
            _amazonDynamoDbConfig.AuthenticationRegion = options.AuthenticationRegion;

        if (options?.ServiceUrl is not null)
            _amazonDynamoDbConfig.ServiceURL = options.ServiceUrl;

        _executionStrategy = executionStrategy.NotNull();
    }

    public IAmazonDynamoDB Client
    {
        get
        {
            field ??= new AmazonDynamoDBClient(_amazonDynamoDbConfig);
            return field;
        }
    }

    public IAsyncEnumerable<Dictionary<string, AttributeValue>> ExecutePartiQl(
        ExecuteStatementRequest statementRequest)
        => new DynamoAsyncEnumerable(this, statementRequest);

    private sealed class DynamoAsyncEnumerable(
        DynamoClientWrapper dynamoClientWrapper,
        ExecuteStatementRequest statementRequest)
        : IAsyncEnumerable<Dictionary<string, AttributeValue>>
    {
        private readonly DynamoClientWrapper _dynamoClientWrapper = dynamoClientWrapper;
        private readonly ExecuteStatementRequest _statementRequest = statementRequest;

        public IAsyncEnumerator<Dictionary<string, AttributeValue>> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
            => new AsyncEnumerator(this, cancellationToken);

        private sealed class AsyncEnumerator(
            DynamoAsyncEnumerable dynamoEnumerable,
            CancellationToken cancellationToken)
            : IAsyncEnumerator<Dictionary<string, AttributeValue>>
        {
            private List<Dictionary<string, AttributeValue>>? _currentItems;
            private int _currentIndex = -1;
            private string? _nextToken;
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
                var request = dynamoEnumerable._statementRequest;
                if (_nextToken is not null)
                    request.NextToken = _nextToken;

                var response =
                    await dynamoEnumerable
                        ._dynamoClientWrapper.Client.ExecuteStatementAsync(request, ct)
                        .ConfigureAwait(false);

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
}
