using System.Collections;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public partial class DynamoShapedQueryCompilingExpressionVisitor
{
    private sealed class QueryingEnumerable<T>(
        DynamoQueryContext queryContext,
        IDynamoClientWrapper client,
        string partiQl,
        IReadOnlyList<AttributeValue> parameters,
        Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T> shaper,
        bool standAloneStateManager,
        bool threadSafetyChecksEnabled) : IEnumerable<T>, IAsyncEnumerable<T>
    {
        private readonly IDynamoClientWrapper _client = client;
        private readonly IReadOnlyList<AttributeValue> _parameters = parameters;
        private readonly string _partiQl = partiQl;
        private readonly DynamoQueryContext _queryContext = queryContext;

        private readonly Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T> _shaper =
            shaper;

        private readonly bool _standAloneStateManager = standAloneStateManager;
        private readonly bool _threadSafetyChecksEnabled = threadSafetyChecksEnabled;

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            _queryContext.CancellationToken = cancellationToken;
            return new AsyncEnumerator(this);
        }

        public IEnumerator<T> GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class Enumerator : IEnumerator<T>
        {
            private readonly IDynamoClientWrapper _client;

            private readonly IConcurrencyDetector? _concurrencyDetector;
            private readonly IReadOnlyList<AttributeValue> _parameters;
            private readonly string _partiQl;
            private readonly DynamoQueryContext _queryContext;

            private readonly Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T>
                _shaper;

            private readonly bool _standAloneStateManager;
            private int _index = -1;

            private List<Dictionary<string, AttributeValue>>? _items;

            public Enumerator(QueryingEnumerable<T> enumerable)
            {
                _queryContext = enumerable._queryContext;
                _client = enumerable._client;
                _partiQl = enumerable._partiQl;
                _parameters = enumerable._parameters;
                _shaper = enumerable._shaper;
                _standAloneStateManager = enumerable._standAloneStateManager;
                _concurrencyDetector = enumerable._threadSafetyChecksEnabled
                    ? _queryContext.ConcurrencyDetector
                    : null;
            }

            public T Current { get; private set; } = default!;

            object IEnumerator.Current => Current!;

            public bool MoveNext()
            {
                using var _ = _concurrencyDetector?.EnterCriticalSection();

                if (_items == null)
                    _queryContext.ExecutionStrategy.Execute(
                        this,
                        (_, _) => InitializeItems(),
                        null);

                _index++;
                if (_items != null && _index < _items.Count)
                {
                    Current = _shaper(_queryContext, _items[_index]);
                    return true;
                }

                return false;
            }

            public void Dispose() { }

            public void Reset() => throw new NotSupportedException();

            private bool InitializeItems()
            {
                var query = new PartiQlQuery(_partiQl, _parameters.ToList());
                _items = _client.ExecutePartiQl<T>(query).GetAwaiter().GetResult();
                _queryContext.InitializeStateManager(_standAloneStateManager);
                return false;
            }
        }

        private sealed class AsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly IDynamoClientWrapper _client;

            private readonly IConcurrencyDetector? _concurrencyDetector;
            private readonly IReadOnlyList<AttributeValue> _parameters;
            private readonly string _partiQl;
            private readonly DynamoQueryContext _queryContext;

            private readonly Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T>
                _shaper;

            private readonly bool _standAloneStateManager;
            private int _index = -1;

            private List<Dictionary<string, AttributeValue>>? _items;

            public AsyncEnumerator(QueryingEnumerable<T> enumerable)
            {
                _queryContext = enumerable._queryContext;
                _client = enumerable._client;
                _partiQl = enumerable._partiQl;
                _parameters = enumerable._parameters;
                _shaper = enumerable._shaper;
                _standAloneStateManager = enumerable._standAloneStateManager;
                _concurrencyDetector = enumerable._threadSafetyChecksEnabled
                    ? _queryContext.ConcurrencyDetector
                    : null;
            }

            public T Current { get; private set; } = default!;

            public async ValueTask<bool> MoveNextAsync()
            {
                using var _ = _concurrencyDetector?.EnterCriticalSection();

                if (_items == null)
                    await _queryContext.ExecutionStrategy.ExecuteAsync(
                        this,
                        (_, _, ct) => InitializeItemsAsync(ct),
                        null,
                        _queryContext.CancellationToken);

                _index++;
                if (_items != null && _index < _items.Count)
                {
                    Current = _shaper(_queryContext, _items[_index]);
                    return true;
                }

                return false;
            }

            public ValueTask DisposeAsync() => default;

            private async Task<bool> InitializeItemsAsync(CancellationToken cancellationToken)
            {
                var query = new PartiQlQuery(_partiQl, _parameters.ToList());
                _items = await _client.ExecutePartiQl<T>(query).ConfigureAwait(false);
                _queryContext.InitializeStateManager(_standAloneStateManager);
                return false;
            }
        }
    }
}
