using System.Diagnostics.CodeAnalysis;

namespace Microsoft.EntityFrameworkCore;

/// <summary>Represents one DynamoDB page of query results.</summary>
/// <remarks>
///     <para>
///         <see cref="NextToken"/> is opaque and should be treated as an implementation detail.
///     </para>
///     <para>
///         End-of-results is determined by <c>NextToken == null</c>.
///     </para>
/// </remarks>
[Experimental("EF9102")]
public readonly struct DynamoPage<T>(IReadOnlyList<T> items, string? nextToken)
{
    /// <summary>The items returned in this page.</summary>
    public IReadOnlyList<T> Items { get; } =
        items ?? throw new ArgumentNullException(nameof(items));

    /// <summary>The opaque token for the next page, or <c>null</c> when no more results exist.</summary>
    public string? NextToken { get; } = nextToken;

    /// <summary>Gets whether another page may be available.</summary>
    public bool HasMoreResults => NextToken is not null;
}
