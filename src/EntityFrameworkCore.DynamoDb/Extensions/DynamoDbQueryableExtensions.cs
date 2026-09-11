using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace Microsoft.EntityFrameworkCore;

/// <summary>DynamoDB-specific extension methods for LINQ queries.</summary>
public static class DynamoDbQueryableExtensions
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
    /// <param name="source">The DynamoDB query source.</param>
    /// <param name="limit">The DynamoDB evaluation budget for this request. Must be positive.</param>
    /// <param name="nextToken">
    ///     An optional continuation token. Empty/whitespace values are treated as <c>null</c>.
    /// </param>
    /// <param name="cancellationToken">A token to observe while awaiting execution.</param>
    /// <returns>A single DynamoDB page result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when this <see cref="IQueryable{TEntity}" /> source is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="limit" /> is zero or negative.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the query provider is not async-capable.</exception>
    [Experimental("EF9102")]
    public static Task<DynamoPage<TEntity>> ToPageAsync<TEntity>(
        this IQueryable<TEntity> source,
        int limit,
        string? nextToken,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);

        if (limit <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                "Limit must be a positive integer.");

        nextToken = string.IsNullOrWhiteSpace(nextToken) ? null : nextToken;

        if (source.Provider is not IAsyncQueryProvider provider)
            throw new InvalidOperationException(
                "The provider for the source 'IQueryable' doesn't implement 'IAsyncQueryProvider'. "
                + "Only providers that implement 'IAsyncQueryProvider' can be used for Entity Framework "
                + "asynchronous operations.");

        return provider.ExecuteAsync<Task<DynamoPage<TEntity>>>(
            Expression.Call(
                null,
                DynamoQueryableMethods.ToPageAsync.MakeGenericMethod(typeof(TEntity)),
                [
                    source.Expression,
                    Expression.Constant(limit, typeof(int)),
                    Expression.Constant(nextToken, typeof(string)),
                    Expression.Constant(default(CancellationToken), typeof(CancellationToken))
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
    /// <param name="source">The DynamoDB query source.</param>
    /// <param name="nextToken">The continuation token to seed on the first request.</param>
    /// <returns>A new query configured with the seed token.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when this <see cref="IQueryable{TEntity}" /> source or <paramref name="nextToken" /> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="nextToken" /> is empty or whitespace.
    /// </exception>
    public static IQueryable<TEntity> WithNextToken<TEntity>(
        this IQueryable<TEntity> source,
        string nextToken)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(nextToken);

        if (string.IsNullOrWhiteSpace(nextToken))
            throw new ArgumentException("Next token must not be empty.", nameof(nextToken));

        return source.Provider is EntityQueryProvider
            ? source.Provider.CreateQuery<TEntity>(
                Expression.Call(
                    null,
                    DynamoQueryableMethods.WithNextToken.MakeGenericMethod(typeof(TEntity)),
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
    /// <param name="source">The DynamoDB query source.</param>
    /// <param name="limit">The maximum number of items DynamoDB should evaluate. Must be positive.</param>
    /// <returns>A new query with the specified evaluation budget.</returns>
    /// <exception cref="ArgumentNullException">Thrown when this <see cref="IQueryable{TEntity}" /> source is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="limit"/> is zero or negative. For a source with no DynamoDB
    ///     query provider, thrown immediately from this call. For a DynamoDB-backed query, the
    ///     provider always rejects a non-positive limit deterministically, but not necessarily from
    ///     this call: a constant is rejected when the query is translated, and a limit sourced from
    ///     a variable or other runtime-computed expression is rejected when its value is resolved
    ///     (query translation or execution, whichever is later).
    /// </exception>
    public static IQueryable<TEntity> Limit<TEntity>(this IQueryable<TEntity> source, int limit)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);

        // A source with no EntityQueryProvider (e.g. an in-memory IQueryable) never reaches the
        // provider's translation/execution pipeline, so this is the only place that can ever
        // validate it — reject eagerly here. For an EntityQueryProvider-backed query, always build
        // the expression and let the existing pipeline validate: DynamoQueryableMethodTranslating
        // ExpressionVisitor rejects an invalid constant during translation, and QueryingEnumerable
        // rejects an invalid resolved value (constant or parameterized) during execution. Eager
        // rejection here as well would misfire during EF Core's precompiled-query interpretation
        // pass, which probes captured locals with a placeholder value (observed: 0) to discover
        // compiled-query parameters.
        if (source.Provider is not EntityQueryProvider)
        {
            if (limit <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(limit),
                    "Limit must be a positive integer.");

            return source;
        }

        return source.Provider.CreateQuery<TEntity>(
            Expression.Call(
                null,
                DynamoQueryableMethods.Limit.MakeGenericMethod(typeof(TEntity)),
                source.Expression,
                Expression.Constant(limit, typeof(int))));
    }

    /// <summary>Requests strongly consistent reads for this query.</summary>
    /// <param name="source">The DynamoDB query source.</param>
    /// <returns>A new query configured for strongly consistent reads.</returns>
    public static IQueryable<TEntity> WithConsistentRead<TEntity>(this IQueryable<TEntity> source)
        where TEntity : class
        => source.WithConsistentRead(true);

    /// <summary>Configures whether this query should use strongly consistent reads.</summary>
    /// <param name="source">The DynamoDB query source.</param>
    /// <param name="consistentRead">Whether this query should use strong consistency.</param>
    /// <returns>A new query with the specified read consistency preference.</returns>
    /// <exception cref="ArgumentNullException">Thrown when this <see cref="IQueryable{TEntity}" /> source is null.</exception>
    public static IQueryable<TEntity> WithConsistentRead<TEntity>(
        this IQueryable<TEntity> source,
        bool consistentRead)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Provider is EntityQueryProvider
            ? source.Provider.CreateQuery<TEntity>(
                Expression.Call(
                    null,
                    DynamoQueryableMethods.WithConsistentRead
                        .MakeGenericMethod(typeof(TEntity)),
                    source.Expression,
                    Expression.Constant(consistentRead, typeof(bool))))
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
    /// <param name="source">The DynamoDB query source.</param>
    /// <returns>A new query that forces base-table execution.</returns>
    /// <exception cref="ArgumentNullException">Thrown when this <see cref="IQueryable{TEntity}" /> source is null.</exception>
    public static IQueryable<TEntity> WithoutIndex<TEntity>(this IQueryable<TEntity> source)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Provider is EntityQueryProvider
            ? source.Provider.CreateQuery<TEntity>(
                Expression.Call(
                    null,
                    DynamoQueryableMethods.WithoutIndex.MakeGenericMethod(typeof(TEntity)),
                    source.Expression))
            : source;
    }

    /// <summary>Bypasses the provider's <c>First*</c> safety validation for this query.</summary>
    /// <remarks>
    ///     This is an intentional escape hatch for callers who accept DynamoDB's single-request
    ///     <c>Limit=1</c> semantics, including queries without partition-key equality. It does not
    ///     allow <c>WithNextToken(...)</c> with <c>First*</c> and does not change execution semantics.
    /// </remarks>
    /// <param name="source">The DynamoDB query source.</param>
    /// <returns>A new query with <c>First*</c> safety validation bypassed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when this <see cref="IQueryable{TEntity}" /> source is null.</exception>
    public static IQueryable<TEntity> AsUnsafeFilteredQuery<TEntity>(this IQueryable<TEntity> source)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Provider is EntityQueryProvider
            ? source.Provider.CreateQuery<TEntity>(
                Expression.Call(
                    null,
                    DynamoQueryableMethods.AsUnsafeFilteredQuery.MakeGenericMethod(
                        typeof(TEntity)),
                    source.Expression))
            : source;
    }

    /// <summary>Allows this query to execute even when it is classified as scan-like.</summary>
    /// <remarks>
    ///     This is a per-query opt-in for intentional scans. It does not change the global scan-query
    ///     behavior configured on the context options.
    /// </remarks>
    /// <param name="source">The DynamoDB query source.</param>
    /// <returns>A new query with scan-like query protection bypassed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when this <see cref="IQueryable{TEntity}" /> source is null.</exception>
    public static IQueryable<TEntity> AllowScan<TEntity>(this IQueryable<TEntity> source)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Provider is EntityQueryProvider
            ? source.Provider.CreateQuery<TEntity>(
                Expression.Call(
                    null,
                    DynamoQueryableMethods.AllowScan.MakeGenericMethod(typeof(TEntity)),
                    source.Expression))
            : source;
    }

    /// <summary>Explicitly selects the DynamoDB secondary index to target for this query.</summary>
    /// <remarks>
    ///     This API records the user's preferred access path and is intended for provider-specific query
    ///     routing. The configured index must exist on the mapped table and be compatible with the final
    ///     query shape.
    /// </remarks>
    /// <param name="source">The DynamoDB query source.</param>
    /// <param name="indexName">The name of the secondary index to target.</param>
    /// <returns>A new query that carries the selected index hint.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when this <see cref="IQueryable{TEntity}" /> source or <paramref name="indexName" /> is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="indexName" /> is empty.</exception>
    public static IQueryable<TEntity> WithIndex<TEntity>(
        this IQueryable<TEntity> source,
        [NotParameterized] string indexName)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(indexName);

        if (string.IsNullOrWhiteSpace(indexName))
            throw new ArgumentException("Index name must not be empty.", nameof(indexName));

        return source.Provider is EntityQueryProvider
            ? source.Provider.CreateQuery<TEntity>(
                Expression.Call(
                    null,
                    DynamoQueryableMethods.WithIndex.MakeGenericMethod(typeof(TEntity)),
                    source.Expression,
                    Expression.Constant(indexName, typeof(string))))
            : source;
    }
}
