namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure;

/// <summary>
///     Defines how DynamoDB pagination behaves for queries with result limits (First, Take,
///     etc.).
/// </summary>
public enum DynamoPaginationMode
{
    /// <summary>
    ///     Smart default: continues paging for queries with result limits (First, Take, Single),
    ///     stops for queries without termination operators. This provides correct EF Core semantics for
    ///     most queries while avoiding unnecessary requests for open-ended queries.
    /// </summary>
    Auto = 0,

    /// <summary>
    ///     Always continues paging until the required number of results is obtained or no more data
    ///     exists. This ensures correct EF Core semantics but may result in many requests for selective
    ///     filters.
    /// </summary>
    Always = 1,

    /// <summary>
    ///     Never continues paging after the first request. Stops after the first page even if fewer
    ///     results than requested are returned. Use with caution - this violates EF Core semantics and may
    ///     return incorrect results.
    /// </summary>
    Never = 2,
}
