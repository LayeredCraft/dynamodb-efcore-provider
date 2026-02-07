using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace Microsoft.EntityFrameworkCore;

/// <summary>DynamoDB-specific extension methods for LINQ queries.</summary>
public static class DynamoDbQueryableExtensions
{
    /// <summary>
    ///     Sets the maximum number of items DynamoDB should evaluate per request for this query.
    ///     Allows per-query override of the global default page size.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         DynamoDB's Limit parameter controls how many items to evaluate (scan), not how many to
    ///         return after filtering. This setting allows you to control request sizes for performance
    ///         tuning.
    ///     </para>
    ///     <para>
    ///         This setting only affects the page size. The result limit (from First, Take, etc.) is
    ///         enforced by continuing to page until enough results are obtained.
    ///     </para>
    ///     <para>
    ///         Example: For a query with a selective non-key filter, using WithPageSize(50) will scan 50
    ///         items per request and continue paging until the result limit is reached.
    ///     </para>
    /// </remarks>
    /// <typeparam name="TEntity">The type of entity being queried.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="pageSize">Maximum items to evaluate per request. Must be positive.</param>
    /// <returns>A new query with the specified page size.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when pageSize is not positive.</exception>
    public static IQueryable<TEntity> WithPageSize<TEntity>(
        this IQueryable<TEntity> source,
        int pageSize) where TEntity : class
    {
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be positive.");

        return source.Provider is EntityQueryProvider
            ? source.Provider.CreateQuery<TEntity>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<TEntity>, int, IQueryable<TEntity>>)WithPageSize).Method,
                    source.Expression,
                    Expression.Constant(pageSize, typeof(int))))
            : source;
    }

    /// <summary>
    ///     Disables automatic pagination continuation for this query. The query will stop after the
    ///     first request even if fewer results are returned.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         WARNING: This violates EF Core semantics. FirstOrDefault may return null even when
    ///         matching data exists on later pages. Only use this if you understand the implications and
    ///         need the performance optimization.
    ///     </para>
    ///     <para>
    ///         This is useful when you know your query will match in the first page, or when you're
    ///         willing to accept potentially incomplete results for better performance.
    ///     </para>
    ///     <para>
    ///         Common use case: Queries with very selective key filters where you're confident the first
    ///         page will contain matches.
    ///     </para>
    /// </remarks>
    /// <typeparam name="TEntity">The type of entity being queried.</typeparam>
    /// <param name="source">The source query.</param>
    /// <returns>A new query with pagination disabled.</returns>
    public static IQueryable<TEntity> WithoutPagination<TEntity>(this IQueryable<TEntity> source)
        where TEntity : class
        => source.Provider is EntityQueryProvider
            ? source.Provider.CreateQuery<TEntity>(
                Expression.Call(
                    null,
                    ((Func<IQueryable<TEntity>, IQueryable<TEntity>>)WithoutPagination).Method,
                    source.Expression))
            : source;
}
