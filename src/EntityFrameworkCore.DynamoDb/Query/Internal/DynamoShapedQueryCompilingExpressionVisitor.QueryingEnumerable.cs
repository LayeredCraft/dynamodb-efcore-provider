using System.Collections;
using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Diagnostics.Internal;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Represents the DynamoShapedQueryCompilingExpressionVisitor type.</summary>
public partial class DynamoShapedQueryCompilingExpressionVisitor
{
    private sealed class QueryingEnumerable<T>(
        DynamoQueryContext queryContext,
        SelectExpression selectExpression,
        IDynamoQuerySqlGeneratorFactory sqlGeneratorFactory,
        Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T> shaper,
        bool standAloneStateManager,
        bool threadSafetyChecksEnabled) : IEnumerable<T>, IAsyncEnumerable<T>, IQueryingEnumerable
    {
        private readonly IDynamoClientWrapper _client = queryContext.Client;

        private readonly IDiagnosticsLogger<DbLoggerCategory.Database.Command> _commandLogger =
            queryContext.CommandDiagnosticsLogger;

        private readonly DynamoQueryContext _queryContext = queryContext;

        private readonly SelectExpression _selectExpression = selectExpression;
        private readonly IDynamoQuerySqlGeneratorFactory _sqlGeneratorFactory = sqlGeneratorFactory;

        private readonly Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T> _shaper =
            shaper;

        private readonly bool _standAloneStateManager = standAloneStateManager;
        private readonly bool _threadSafetyChecksEnabled = threadSafetyChecksEnabled;

        /// <summary>Provides functionality for this member.</summary>
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new AsyncEnumerator(this, cancellationToken);

        /// <summary>Provides functionality for this member.</summary>
        public IEnumerator<T> GetEnumerator()
            => throw new InvalidOperationException(
                "Sync enumerating is not supported for DynamoDB.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Provides functionality for this member.</summary>
        public string ToQueryString() => throw new NotImplementedException();

        /// <summary>Generates the PartiQL query at runtime with parameter values.</summary>
        private DynamoPartiQlQuery GenerateQuery()
            => _sqlGeneratorFactory.Create().Generate(_selectExpression, _queryContext.Parameters);

        private sealed class AsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly CancellationToken _cancellationToken;
            private readonly IConcurrencyDetector? _concurrencyDetector;
            private readonly DynamoQueryContext _queryContext;
            private readonly QueryingEnumerable<T> _queryingEnumerable;

            private readonly Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T>
                _shaper;

            private readonly bool _standAloneStateManager;

            /// <summary>
            ///     The user-specified (or implicit) evaluation limit resolved from
            ///     <c>SelectExpression.LimitExpression</c>. Maps directly to <c>ExecuteStatementRequest.Limit</c>.
            /// </summary>
            private readonly int? _limit;

            /// <summary>
            ///     True when the query should stop after the first executed request — either because it is a
            ///     <c>First*</c> terminal or because the user set an explicit <c>Limit(n)</c>.
            /// </summary>
            private readonly bool _singlePageOnly;

            private IAsyncEnumerator<Dictionary<string, AttributeValue>>? _dataEnumerator;

            /// <summary>Provides functionality for this member.</summary>
            public AsyncEnumerator(
                QueryingEnumerable<T> enumerable,
                CancellationToken cancellationToken)
            {
                _queryingEnumerable = enumerable;
                _queryContext = enumerable._queryContext;
                _shaper = enumerable._shaper;
                _standAloneStateManager = enumerable._standAloneStateManager;
                _cancellationToken = cancellationToken;

                // Resolve the evaluation limit from the SelectExpression.
                _limit = ResolveIntExpression(
                    enumerable._selectExpression.LimitExpression,
                    enumerable._selectExpression.Limit,
                    _queryContext);

                if (_limit is <= 0)
                    throw new ArgumentOutOfRangeException(
                        "limit",
                        "Limit must be a positive integer.");

                // Single-page when: First* terminal (always one request) OR user set Limit(n)
                // (per ADR-002, Limit(n) is always a single request).
                _singlePageOnly = enumerable._selectExpression.IsFirstTerminal
                    || enumerable._selectExpression.HasUserLimit;

                _concurrencyDetector = enumerable._threadSafetyChecksEnabled
                    ? _queryContext.ConcurrencyDetector
                    : null;
            }

            /// <summary>Provides functionality for this member.</summary>
            public T Current { get; private set; } = default!;

            /// <summary>Provides functionality for this member.</summary>
            public async ValueTask<bool> MoveNextAsync()
            {
                using var _ = _concurrencyDetector?.EnterCriticalSection();

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
                            Statement = sqlQuery.Sql,
                            Parameters = sqlQuery.Parameters.ToList(),
                            // Maps directly to ExecuteStatementRequest.Limit (evaluation budget).
                            Limit = _limit,
                        },
                        _singlePageOnly);

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

                return true;
            }

            /// <summary>Provides functionality for this member.</summary>
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
