using System.Collections;
using System.Linq.Expressions;
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
                _resultLimit = ResolveIntExpression(
                    enumerable._selectExpression.ResultLimitExpression,
                    enumerable._selectExpression.ResultLimit,
                    _queryContext);

                _pageSize = ResolveIntExpression(
                    enumerable._selectExpression.PageSizeExpression,
                    enumerable._selectExpression.PageSize,
                    _queryContext);

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

                    if (_resultLimit is { } resultLimit && _pageSize is null)
                        _queryingEnumerable._commandLogger.RowLimitingQueryWithoutPageSize(
                            resultLimit);

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

            private static int? ResolveIntExpression(
                Expression? expression,
                int? fallback,
                DynamoQueryContext queryContext)
            {
                if (expression is null)
                    return fallback;

                if (expression is ConstantExpression { Value: int constantValue })
                    return constantValue;

                if (expression is QueryParameterExpression parameterExpression)
                    return Convert.ToInt32(queryContext.Parameters[parameterExpression.Name]);

                throw new InvalidOperationException(
                    "Limit expression must be normalized before execution.");
            }
        }
    }
}
