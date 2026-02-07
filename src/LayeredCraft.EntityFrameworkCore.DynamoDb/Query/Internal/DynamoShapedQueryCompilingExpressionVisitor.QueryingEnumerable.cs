using System.Collections;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Diagnostics.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public partial class DynamoShapedQueryCompilingExpressionVisitor
{
    private sealed class QueryingEnumerable<T>(
        DynamoQueryContext queryContext,
        SelectExpression selectExpression,
        DynamoQuerySqlGenerator sqlGenerator,
        Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T> shaper,
        bool standAloneStateManager,
        bool threadSafetyChecksEnabled,
        bool paginationDisabled) : IEnumerable<T>, IAsyncEnumerable<T>, IQueryingEnumerable
    {
        private readonly IDynamoClientWrapper _client = queryContext.Client;

        private readonly IDiagnosticsLogger<DbLoggerCategory.Database.Command> _commandLogger =
            queryContext.CommandDiagnosticsLogger;

        private readonly DynamoQueryContext _queryContext = queryContext;

        private readonly SelectExpression _selectExpression = selectExpression;

        private readonly Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T> _shaper =
            shaper;

        private readonly bool _standAloneStateManager = standAloneStateManager;
        private readonly bool _threadSafetyChecksEnabled = threadSafetyChecksEnabled;
        private readonly bool _paginationDisabled = paginationDisabled;

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new AsyncEnumerator(this, cancellationToken);

        public IEnumerator<T> GetEnumerator()
            => throw new InvalidOperationException(
                "Sync enumerating is not supported for DynamoDB.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public string ToQueryString() => throw new NotImplementedException();

        /// <summary>Generates the PartiQL query at runtime with parameter values.</summary>
        private DynamoPartiQlQuery GenerateQuery()
            => sqlGenerator.Generate(_selectExpression, _queryContext.Parameters);

        private sealed class AsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly CancellationToken _cancellationToken;
            private readonly IConcurrencyDetector? _concurrencyDetector;
            private readonly DynamoQueryContext _queryContext;
            private readonly QueryingEnumerable<T> _queryingEnumerable;

            private readonly Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T>
                _shaper;

            private readonly bool _standAloneStateManager;
            private readonly int? _resultLimit;
            private readonly int? _pageSize;
            private readonly bool _paginationDisabled;
            private int _returnedCount;

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

                // Separate result limit (how many to return) from page size (how many to scan)
                // Evaluate ResultLimitExpression if set (handles parameterized Take)
                if (enumerable._selectExpression.ResultLimitExpression != null)
                    // Evaluate the expression with parameter values from QueryContext
                    _resultLimit = ParameterExpressionEvaluator.EvaluateInt(
                        enumerable._selectExpression.ResultLimitExpression,
                        _queryContext.Parameters);
                else
                    _resultLimit = enumerable._selectExpression.ResultLimit;

                _pageSize = enumerable._selectExpression.PageSizeExpression is not null
                    ? ParameterExpressionEvaluator.EvaluateInt(
                        enumerable._selectExpression.PageSizeExpression,
                        _queryContext.Parameters)
                    : enumerable._selectExpression.PageSize;

                if (_pageSize is <= 0)
                    throw new InvalidOperationException(
                        "WithPageSize must evaluate to a positive integer.");
                _paginationDisabled = enumerable._paginationDisabled;

                _concurrencyDetector = enumerable._threadSafetyChecksEnabled
                    ? _queryContext.ConcurrencyDetector
                    : null;
            }

            public T Current { get; private set; } = default!;

            public async ValueTask<bool> MoveNextAsync()
            {
                using var _ = _concurrencyDetector?.EnterCriticalSection();

                if (_resultLimit.HasValue && _returnedCount >= _resultLimit.Value)
                    return false;

                if (_dataEnumerator == null)
                {
                    // Generate query at runtime with parameter values inlined
                    var sqlQuery = _queryingEnumerable.GenerateQuery();

                    _queryingEnumerable._commandLogger.ExecutingPartiQlQuery(
                        _queryingEnumerable._selectExpression.TableName,
                        sqlQuery.Sql);

                    var asyncEnumerable = _queryingEnumerable._client.ExecutePartiQl(
                        new ExecuteStatementRequest
                        {
                            Statement =
                                sqlQuery.Sql,
                            Parameters = sqlQuery.Parameters.ToList(),
                            Limit = _pageSize, // Use page size for DynamoDB scan limit
                        },
                        _paginationDisabled);

                    _dataEnumerator = asyncEnumerable.GetAsyncEnumerator(_cancellationToken);
                    _queryContext.InitializeStateManager(_standAloneStateManager);
                }

                var hasNext = await _dataEnumerator.MoveNextAsync().ConfigureAwait(false);

                if (!hasNext)
                {
                    Current = default!;
                    return false;
                }

                Current = _shaper(_queryContext, _dataEnumerator.Current);
                _returnedCount++;

                return true;
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
