using System.Collections;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public partial class DynamoShapedQueryCompilingExpressionVisitor
{
    private sealed class QueryingEnumerable<T>(
        DynamoQueryContext queryContext,
        IDynamoClientWrapper client,
        SelectExpression selectExpression,
        DynamoQuerySqlGenerator sqlGenerator,
        ISqlExpressionFactory sqlExpressionFactory,
        Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T> shaper,
        bool standAloneStateManager,
        bool threadSafetyChecksEnabled) : IEnumerable<T>, IAsyncEnumerable<T>, IQueryingEnumerable
    {
        private readonly IDynamoClientWrapper _client = client;
        private readonly SelectExpression _selectExpression = selectExpression;
        private readonly DynamoQuerySqlGenerator _sqlGenerator = sqlGenerator;
        private readonly ISqlExpressionFactory _sqlExpressionFactory = sqlExpressionFactory;
        private readonly DynamoQueryContext _queryContext = queryContext;

        private readonly Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T> _shaper =
            shaper;

        private readonly bool _standAloneStateManager = standAloneStateManager;
        private readonly bool _threadSafetyChecksEnabled = threadSafetyChecksEnabled;

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new AsyncEnumerator(this, cancellationToken);

        public IEnumerator<T> GetEnumerator()
            => throw new InvalidOperationException(
                "Sync enumerating is not supported for DynamoDB.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public string ToQueryString() => throw new NotImplementedException();

        /// <summary>
        /// Generates the PartiQL query at runtime, inlining parameter values.
        /// </summary>
        private DynamoPartiQlQuery GenerateQuery()
        {
            // Inline parameters before SQL generation
            var inlinedSelectExpression = (SelectExpression)new ParameterInliner(
                _sqlExpressionFactory,
                _queryContext.Parameters).Visit(_selectExpression);

            // Generate SQL from the inlined expression
            return _sqlGenerator.Generate(inlinedSelectExpression, _queryContext.Parameters);
        }

        private sealed class AsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly QueryingEnumerable<T> _queryingEnumerable;
            private readonly IConcurrencyDetector? _concurrencyDetector;
            private readonly DynamoQueryContext _queryContext;
            private readonly bool _standAloneStateManager;

            private readonly Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T>
                _shaper;

            private readonly CancellationToken _cancellationToken;

            private IAsyncEnumerator<Dictionary<string, AttributeValue>>? _dataEnumerator;

            public AsyncEnumerator(
                QueryingEnumerable<T> enumerable,
                CancellationToken cancellationToken)
            {
                _queryingEnumerable = enumerable;
                _queryContext = enumerable._queryContext;
                _shaper = enumerable._shaper;
                _standAloneStateManager = enumerable._standAloneStateManager;
                _cancellationToken = cancellationToken;
                _concurrencyDetector = enumerable._threadSafetyChecksEnabled
                    ? _queryContext.ConcurrencyDetector
                    : null;
            }

            public T Current { get; private set; } = default!;

            public async ValueTask<bool> MoveNextAsync()
            {
                using var _ = _concurrencyDetector?.EnterCriticalSection();

                if (_dataEnumerator == null)
                {
                    // Generate query at runtime with parameter values inlined
                    var sqlQuery = _queryingEnumerable.GenerateQuery();

                    var asyncEnumerable = _queryingEnumerable._client.ExecutePartiQl(
                        new ExecuteStatementRequest
                        {
                            Statement = sqlQuery.Sql, Parameters = sqlQuery.Parameters.ToList(),
                        });

                    _dataEnumerator = asyncEnumerable.GetAsyncEnumerator(_cancellationToken);
                    _queryContext.InitializeStateManager(_standAloneStateManager);
                }

                var hasNext = await _dataEnumerator.MoveNextAsync().ConfigureAwait(false);

                Current = hasNext ? _shaper(_queryContext, _dataEnumerator.Current) : default!;

                return hasNext;
            }

            public ValueTask DisposeAsync()
            {
                var enumerator = _dataEnumerator;
                if (enumerator == null)
                    return default;

                _dataEnumerator = null;
                return enumerator.DisposeAsync();
            }
        }
    }
}
