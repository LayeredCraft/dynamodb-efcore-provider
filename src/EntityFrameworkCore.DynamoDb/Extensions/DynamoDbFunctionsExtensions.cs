using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.Extensions;

/// <summary>
///     DynamoDB-specific <c>DbFunctions</c> extensions for IS NULL, IS MISSING, and string range
///     predicates. These methods are translated server-side and must not be called directly in
///     application code.
/// </summary>
public static class DynamoDbFunctionsExtensions
{
    /// <summary>Tests that an attribute has the DynamoDB NULL type. Translated to <c>"attr" IS NULL</c>.</summary>
    /// <returns>Never returns; throws at runtime.</returns>
    /// <exception cref="InvalidOperationException">Always thrown — server-side only.</exception>
    public static bool IsNull(this DbFunctions _, object? value)
        => throw new InvalidOperationException(
            "IsNull is a server-side only method and cannot be called directly.");

    /// <summary>
    ///     Tests that an attribute does not have the DynamoDB NULL type. Translated to
    ///     <c>"attr" IS NOT NULL</c>.
    /// </summary>
    /// <returns>Never returns; throws at runtime.</returns>
    /// <exception cref="InvalidOperationException">Always thrown — server-side only.</exception>
    public static bool IsNotNull(this DbFunctions _, object? value)
        => throw new InvalidOperationException(
            "IsNotNull is a server-side only method and cannot be called directly.");

    /// <summary>Tests that an attribute is absent from the item. Translated to <c>"attr" IS MISSING</c>.</summary>
    /// <returns>Never returns; throws at runtime.</returns>
    /// <exception cref="InvalidOperationException">Always thrown — server-side only.</exception>
    public static bool IsMissing(this DbFunctions _, object? value)
        => throw new InvalidOperationException(
            "IsMissing is a server-side only method and cannot be called directly.");

    /// <summary>
    ///     Tests that an attribute is present in the item. Translated to <c>"attr" IS NOT MISSING</c>
    ///     .
    /// </summary>
    /// <returns>Never returns; throws at runtime.</returns>
    /// <exception cref="InvalidOperationException">Always thrown — server-side only.</exception>
    public static bool IsNotMissing(this DbFunctions _, object? value)
        => throw new InvalidOperationException(
            "IsNotMissing is a server-side only method and cannot be called directly.");

    /// <summary>
    ///     Tests that <paramref name="value" /> is lexicographically greater than
    ///     <paramref name="comparison" />. Translated to <c>"attr" &gt; ?</c>.
    /// </summary>
    /// <param name="_">The EF Core database functions instance.</param>
    /// <param name="value">The value expression to compare.</param>
    /// <param name="comparison">The comparison expression or bound.</param>
    /// <returns>Never returns; throws at runtime.</returns>
    /// <exception cref="InvalidOperationException">Always thrown — server-side only.</exception>
    public static bool GreaterThan(this DbFunctions _, string? value, string? comparison)
        => throw new InvalidOperationException(
            "GreaterThan is a server-side only method and cannot be called directly.");

    /// <summary>
    ///     Tests that <paramref name="value" /> is lexicographically less than
    ///     <paramref name="comparison" />. Translated to <c>"attr" &lt; ?</c>.
    /// </summary>
    /// <param name="_">The EF Core database functions instance.</param>
    /// <param name="value">The value expression to compare.</param>
    /// <param name="comparison">The comparison expression or bound.</param>
    /// <returns>Never returns; throws at runtime.</returns>
    /// <exception cref="InvalidOperationException">Always thrown — server-side only.</exception>
    public static bool LessThan(this DbFunctions _, string? value, string? comparison)
        => throw new InvalidOperationException(
            "LessThan is a server-side only method and cannot be called directly.");

    /// <summary>
    ///     Tests that <paramref name="value" /> is lexicographically between inclusive
    ///     <paramref name="low" /> and <paramref name="high" /> bounds. Translated to
    ///     <c>"attr" BETWEEN ? AND ?</c>.
    /// </summary>
    /// <param name="_">The EF Core database functions instance.</param>
    /// <param name="value">The value expression to compare.</param>
    /// <param name="low">The inclusive lower bound expression or value.</param>
    /// <param name="high">The inclusive upper bound expression or value.</param>
    /// <returns>Never returns; throws at runtime.</returns>
    /// <exception cref="InvalidOperationException">Always thrown — server-side only.</exception>
    public static bool Between(this DbFunctions _, string? value, string? low, string? high)
        => throw new InvalidOperationException(
            "Between is a server-side only method and cannot be called directly.");
}
