using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace Microsoft.EntityFrameworkCore;

/// <summary>DynamoDB-specific extension methods for LINQ queries.</summary>
public static class DynamoDbQueryableExtensions
{
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
    /// <param name="source">The query to apply the limit to.</param>
    /// <param name="limit">The maximum number of items DynamoDB should evaluate. Must be positive.</param>
    /// <returns>A new query with the specified evaluation budget.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown immediately when <paramref name="limit"/> is zero or negative and the value is
    ///     known at construction time (constant). For compiled queries with runtime values, thrown at
    ///     execution time.
    /// </exception>
    public static IQueryable<TEntity> Limit<TEntity>(this IQueryable<TEntity> source, int limit)
        where TEntity : class
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
    ///     Permits <c>First*</c> to run on queries that have non-key predicates or scan-like paths
    ///     (no partition-key equality). Without this opt-in, <c>First*</c> is restricted to safe
    ///     key-only queries.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This is a permission flag, not a behavioral change. Execution is unchanged: evaluation
    ///         budget comes from <c>Limit(n)</c> if present, or DynamoDB's 1MB default otherwise.
    ///         Single request. No paging. The caller accepts that the result may be null even when
    ///         matches exist beyond the evaluation budget.
    ///     </para>
    ///     <para>
    ///         Applied to a key-only query with no non-key predicates, this is a silent no-op.
    ///         Applied to <c>ToListAsync()</c> or other multi-result terminals, this is also a silent
    ///         no-op.
    ///     </para>
    /// </remarks>
    /// <param name="source">The query to apply the opt-in to.</param>
    /// <returns>A new query with the non-key filter opt-in flag set.</returns>
    public static IQueryable<TEntity> WithNonKeyFilter<TEntity>(this IQueryable<TEntity> source)
        where TEntity : class
        => source.Provider is EntityQueryProvider
            ? source.Provider.CreateQuery<TEntity>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<TEntity>, IQueryable<TEntity>>)WithNonKeyFilter).Method,
                    source.Expression))
            : source;

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
    public static IQueryable<TEntity> WithoutIndex<TEntity>(this IQueryable<TEntity> source)
        where TEntity : class
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
    public static IQueryable<TEntity> WithIndex<TEntity>(
        this IQueryable<TEntity> source,
        [NotParameterized] string indexName) where TEntity : class
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
