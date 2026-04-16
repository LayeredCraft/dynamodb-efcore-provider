using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace Microsoft.EntityFrameworkCore;

/// <summary>DynamoDB-specific extension methods for LINQ queries.</summary>
public static class DynamoDbQueryableExtensions
{
    /// <param name="source">The query to apply the limit to.</param>
    extension<TEntity>(IQueryable<TEntity> source) where TEntity : class
    {
        /// <summary>
        ///     Executes this query as a single DynamoDB request and returns one page plus a continuation
        ///     token for resuming.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         <paramref name="limit" /> is DynamoDB's evaluation budget (<c>ExecuteStatementRequest.Limit</c>),
        ///         not a guaranteed returned-row count.
        ///     </para>
        ///     <para>
        ///         Completion is determined only by <c>NextToken == null</c>. A page can legitimately return
        ///         fewer than <paramref name="limit" /> items (including zero) and still include a non-null
        ///         continuation token when evaluated items were filtered out.
        ///     </para>
        /// </remarks>
        /// <param name="limit">The DynamoDB evaluation budget for this request. Must be positive.</param>
        /// <param name="nextToken">An optional continuation token. Empty/whitespace values are treated as <c>null</c>.</param>
        /// <param name="cancellationToken">A token to observe while awaiting execution.</param>
        /// <returns>A single DynamoDB page result.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is zero or negative.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the query provider is not async-capable.</exception>
        [Experimental("EF9102")]
        public Task<DynamoPage<TEntity>> ToPageAsync(
            int limit,
            string? nextToken,
            CancellationToken cancellationToken = default)
        {
            if (limit <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(limit),
                    "Limit must be a positive integer.");

            nextToken = string.IsNullOrWhiteSpace(nextToken) ? null : nextToken;

            if (source.Provider is not IAsyncQueryProvider provider)
                throw new InvalidOperationException(
                    "The provider for the source 'IQueryable' doesn't implement 'IAsyncQueryProvider'. "
                    + "Only providers that implement 'IAsyncQueryProvider' can be used for Entity Framework asynchronous operations.");

            return provider.ExecuteAsync<Task<DynamoPage<TEntity>>>(
                Expression.Call(
                    instance: null,
                    method:
                    new Func<IQueryable<TEntity>, int, string?, CancellationToken,
                        Task<DynamoPage<TEntity>>>(ToPageAsync).Method,
                    arguments:
                    [
                        source.Expression,
                        Expression.Constant(limit, typeof(int)),
                        Expression.Constant(nextToken, typeof(string)),
                        Expression.Constant(default(CancellationToken), typeof(CancellationToken)),
                    ]),
                cancellationToken);
        }

        /// <summary>
        ///     Seeds the first request for this query with a DynamoDB continuation token.
        /// </summary>
        /// <remarks>
        ///     This affects only the first request. For example, <c>WithNextToken(token).Limit(n)</c>
        ///     executes one request from the saved cursor with evaluation budget <c>n</c>.
        /// </remarks>
        /// <param name="nextToken">The continuation token to seed on the first request.</param>
        /// <returns>A new query configured with the seed token.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="nextToken"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="nextToken"/> is empty or whitespace.</exception>
        public IQueryable<TEntity> WithNextToken(string nextToken)
        {
            if (nextToken is null)
                throw new ArgumentNullException(nameof(nextToken));

            if (string.IsNullOrWhiteSpace(nextToken))
                throw new ArgumentException("Next token must not be empty.", nameof(nextToken));

            return source.Provider is EntityQueryProvider
                ? source.Provider.CreateQuery<TEntity>(
                    Expression.Call(
                        null,
                        ((Func<IQueryable<TEntity>, string, IQueryable<TEntity>>)WithNextToken)
                        .Method,
                        source.Expression,
                        Expression.Constant(nextToken, typeof(string))))
                : source;
        }

        /// <summary>
        ///     Sets a DynamoDB evaluation budget for this query. DynamoDB evaluates at most
        ///     <paramref name="limit"/> items, applies any non-key filters, and returns
        ///     0..<paramref name="limit"/> results in a single request. There is no paging.
        ///     This maps directly to <c>ExecuteStatementRequest.Limit</c>.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Unlike EF Core's <c>Take(n)</c>, this does not guarantee <paramref name="limit"/> rows
        ///         are returned. It bounds how many items DynamoDB reads. When filters are present, fewer
        ///         items may match within the evaluated range.
        ///     </para>
        ///     <para>
        ///         When chained multiple times, the last call wins.
        ///     </para>
        /// </remarks>
        /// <param name="limit">The maximum number of items DynamoDB should evaluate. Must be positive.</param>
        /// <returns>A new query with the specified evaluation budget.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown immediately when <paramref name="limit"/> is zero or negative and the value is
        ///     known at construction time (constant). For compiled queries with runtime values, thrown at
        ///     execution time.
        /// </exception>
        public IQueryable<TEntity> Limit(int limit)
        {
            if (limit <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(limit),
                    "Limit must be a positive integer.");

            return source.Provider is EntityQueryProvider
                ? source.Provider.CreateQuery<TEntity>(
                    Expression.Call(
                        null,
                        ((Func<IQueryable<TEntity>, int, IQueryable<TEntity>>)Limit).Method,
                        source.Expression,
                        Expression.Constant(limit, typeof(int))))
                : source;
        }

        /// <summary>
        ///     Explicitly suppresses index selection for this query, forcing it to use the base table
        ///     regardless of the configured automatic index selection mode.
        /// </summary>
        /// <remarks>
        ///     Use this when you need a query to hit the base table even though the query shape would
        ///     normally trigger automatic index selection. For example, strongly-consistent reads are
        ///     only available on the base table.
        ///     <para>
        ///         Combining this with <c>.WithIndex()</c> on the same query is a programmer error and
        ///         will throw at query compilation time.
        ///     </para>
        ///     <para>
        ///         When this override is active, a <c>DYNAMO_IDX006</c> diagnostic is emitted at
        ///         <c>Information</c> level so the suppression is visible in query logs.
        ///     </para>
        /// </remarks>
        /// <returns>A new query that forces base-table execution.</returns>
        public IQueryable<TEntity> WithoutIndex()
            => source.Provider is EntityQueryProvider
                ? source.Provider.CreateQuery<TEntity>(
                    Expression.Call(
                        null,
                        ((Func<IQueryable<TEntity>, IQueryable<TEntity>>)WithoutIndex).Method,
                        source.Expression))
                : source;

        /// <summary>Explicitly selects the DynamoDB secondary index to target for this query.</summary>
        /// <remarks>
        ///     This API records the user's preferred access path and is intended for provider-specific query
        ///     routing. The configured index must exist on the mapped table and be compatible with the final
        ///     query shape.
        /// </remarks>
        /// <returns>A new query that carries the selected index hint.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="indexName" /> is empty.</exception>
        public IQueryable<TEntity> WithIndex([NotParameterized] string indexName)
        {
            if (string.IsNullOrWhiteSpace(indexName))
                throw new ArgumentException("Index name must not be empty.", nameof(indexName));

            return source.Provider is EntityQueryProvider
                ? source.Provider.CreateQuery<TEntity>(
                    Expression.Call(
                        null,
                        ((Func<IQueryable<TEntity>, string, IQueryable<TEntity>>)WithIndex).Method,
                        source.Expression,
                        Expression.Constant(indexName, typeof(string))))
                : source;
        }
    }
}
