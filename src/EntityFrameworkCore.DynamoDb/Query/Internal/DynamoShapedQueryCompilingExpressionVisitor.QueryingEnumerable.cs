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

            /// <summary>
            ///     The optional first-request continuation token resolved from
            ///     <c>SelectExpression.SeedNextTokenExpression</c>.
            /// </summary>
            private readonly string? _seedNextToken;

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

                _seedNextToken = ResolveStringExpression(
                    enumerable._selectExpression.SeedNextTokenExpression,
                    enumerable._selectExpression.SeedNextToken,
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
                            Parameters = sqlQuery.Parameters.Count > 0 ? sqlQuery.Parameters.ToList() : null,
                            // Maps directly to ExecuteStatementRequest.Limit (evaluation budget).
                            Limit = _limit,
                            NextToken = _seedNextToken,
                        },
                        _singlePageOnly,
                        // Store the raw response on the query context so the shaper can bind it
                        // to the __executeStatementResponse shadow property of each entity.
                        response => _queryContext.CurrentPageResponse = response);

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
        }
    }

#pragma warning disable EF9102
    private sealed class PagingQueryingEnumerable<T>(
        DynamoQueryContext queryContext,
        SelectExpression selectExpression,
        IDynamoQuerySqlGeneratorFactory sqlGeneratorFactory,
        Func<DynamoQueryContext, Dictionary<string, AttributeValue>, T> shaper,
        bool standAloneStateManager,
        bool threadSafetyChecksEnabled) : IEnumerable<DynamoPage<T>>,
        IAsyncEnumerable<DynamoPage<T>>,
        IQueryingEnumerable
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

        public IAsyncEnumerator<DynamoPage<T>> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
            => new AsyncEnumerator(this, cancellationToken);

        public IEnumerator<DynamoPage<T>> GetEnumerator()
            => throw new InvalidOperationException(
                "Sync enumerating is not supported for DynamoDB.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public string ToQueryString() => throw new NotImplementedException();

        private DynamoPartiQlQuery GenerateQuery()
            => _sqlGeneratorFactory.Create().Generate(_selectExpression, _queryContext.Parameters);

        private sealed class AsyncEnumerator : IAsyncEnumerator<DynamoPage<T>>
        {
            private readonly PagingQueryingEnumerable<T> _queryingEnumerable;
            private readonly DynamoQueryContext _queryContext;
            private readonly CancellationToken _cancellationToken;
            private readonly IConcurrencyDetector? _concurrencyDetector;
            private readonly int _limit;
            private readonly string? _seedNextToken;

            private bool _emitted;

            public AsyncEnumerator(
                PagingQueryingEnumerable<T> queryingEnumerable,
                CancellationToken cancellationToken)
            {
                _queryingEnumerable = queryingEnumerable;
                _queryContext = queryingEnumerable._queryContext;
                _cancellationToken = cancellationToken;

                _limit = ResolveIntExpression(
                        queryingEnumerable._selectExpression.LimitExpression,
                        queryingEnumerable._selectExpression.Limit,
                        _queryContext)
                    ?? throw new InvalidOperationException(
                        "ToPageAsync requires a resolved limit.");

                if (_limit <= 0)
                    throw new ArgumentOutOfRangeException(
                        "limit",
                        "Limit must be a positive integer.");

                _seedNextToken = NormalizeToken(
                    ResolveStringExpression(
                        queryingEnumerable._selectExpression.SeedNextTokenExpression,
                        queryingEnumerable._selectExpression.SeedNextToken,
                        _queryContext));

                _concurrencyDetector = queryingEnumerable._threadSafetyChecksEnabled
                    ? _queryContext.ConcurrencyDetector
                    : null;
            }

            public DynamoPage<T> Current { get; private set; } = default!;

            public async ValueTask<bool> MoveNextAsync()
            {
                using var _ = _concurrencyDetector?.EnterCriticalSection();

                if (_emitted)
                {
                    Current = default!;
                    return false;
                }

                var sqlQuery = _queryingEnumerable.GenerateQuery();

                _queryingEnumerable._commandLogger.ExecutingPartiQlQuery(
                    _queryingEnumerable._selectExpression.TableName,
                    sqlQuery.Sql);

                var items = new List<T>();

                var asyncEnumerable = _queryingEnumerable._client.ExecutePartiQl(
                    new ExecuteStatementRequest
                    {
                        Statement = sqlQuery.Sql,
                        Parameters = sqlQuery.Parameters.Count > 0 ? sqlQuery.Parameters.ToList() : null,
                        Limit = _limit,
                        NextToken = _seedNextToken,
                    },
                    singlePageOnly: true,
                    response => _queryContext.CurrentPageResponse = response);

                _queryContext.InitializeStateManager(_queryingEnumerable._standAloneStateManager);

                await using var dataEnumerator =
                    asyncEnumerable.GetAsyncEnumerator(_cancellationToken);
                while (await dataEnumerator.MoveNextAsync().ConfigureAwait(false))
                    items.Add(_queryingEnumerable._shaper(_queryContext, dataEnumerator.Current));

                var nextToken = NormalizeToken(_queryContext.CurrentPageResponse?.NextToken);
                Current = new DynamoPage<T>(items, nextToken);
                _emitted = true;
                return true;
            }

            public ValueTask DisposeAsync() => default;
        }
    }
#pragma warning restore EF9102

    /// <summary>
    ///     Resolves an integer expression to its runtime value. Handles constant literals and compiled-query
    ///     parameter expressions; falls back to the pre-resolved scalar when no expression is present.
    /// </summary>
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

    /// <summary>
    ///     Resolves a string expression to its runtime value. Handles constant literals and compiled-query
    ///     parameter expressions; falls back to the pre-resolved scalar when no expression is present.
    /// </summary>
    /// <remarks>
    ///     Compiled queries store token arguments as <see cref="QueryParameterExpression" /> nodes rather
    ///     than <see cref="ConstantExpression" /> nodes, so the lookup into
    ///     <c>DynamoQueryContext.Parameters</c> is required for those code paths.
    /// </remarks>
    private static string? ResolveStringExpression(
        Expression? expression,
        string? fallback,
        DynamoQueryContext queryContext)
    {
        if (expression is null)
            return fallback;

        if (expression is ConstantExpression { Value: string constantValue })
            return constantValue;

        if (expression is QueryParameterExpression parameterExpression)
        {
            var value = queryContext.Parameters[parameterExpression.Name];

            // Guard against a wrongly-typed runtime parameter. Without this check, `value as
            // string`
            // silently returns null, which degrades into "start from beginning" with no diagnostic.
            if (value is not null and not string)
                throw new InvalidOperationException(
                    $"Parameter '{parameterExpression.Name}' was expected to be a string (next-token) but was {value.GetType().Name}.");

            return (string?)value;
        }

        throw new InvalidOperationException(
            "Seed next-token expression must be normalized before execution.");
    }

    /// <summary>Normalizes empty or whitespace-only tokens to <see langword="null" />.</summary>
    private static string? NormalizeToken(string? token)
        => string.IsNullOrWhiteSpace(token) ? null : token;
}
