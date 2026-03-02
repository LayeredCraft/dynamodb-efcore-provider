using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;

/// <summary>
///     DynamoDB-specific <see cref="DbFunctions" /> extensions for IS NULL and IS MISSING
///     predicates. These methods are translated server-side and must not be called directly in
///     application code.
/// </summary>
public static class DynamoDbFunctionsExtensions
{
    /// <summary>Tests that an attribute has the DynamoDB NULL type. Translated to <c>"attr" IS NULL</c>.</summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance (not used at runtime).</param>
    /// <param name="value">The attribute value to test.</param>
    /// <returns>Never returns; throws at runtime.</returns>
    /// <exception cref="InvalidOperationException">Always thrown — server-side only.</exception>
    public static bool IsNull(this DbFunctions _, object? value)
        => throw new InvalidOperationException(
            "IsNull is a server-side only method and cannot be called directly.");

    /// <summary>
    ///     Tests that an attribute does not have the DynamoDB NULL type. Translated to
    ///     <c>"attr" IS NOT NULL</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance (not used at runtime).</param>
    /// <param name="value">The attribute value to test.</param>
    /// <returns>Never returns; throws at runtime.</returns>
    /// <exception cref="InvalidOperationException">Always thrown — server-side only.</exception>
    public static bool IsNotNull(this DbFunctions _, object? value)
        => throw new InvalidOperationException(
            "IsNotNull is a server-side only method and cannot be called directly.");

    /// <summary>Tests that an attribute is absent from the item. Translated to <c>"attr" IS MISSING</c>.</summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance (not used at runtime).</param>
    /// <param name="value">The attribute value to test.</param>
    /// <returns>Never returns; throws at runtime.</returns>
    /// <exception cref="InvalidOperationException">Always thrown — server-side only.</exception>
    public static bool IsMissing(this DbFunctions _, object? value)
        => throw new InvalidOperationException(
            "IsMissing is a server-side only method and cannot be called directly.");

    /// <summary>
    ///     Tests that an attribute is present in the item. Translated to <c>"attr" IS NOT MISSING</c>
    ///     .
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance (not used at runtime).</param>
    /// <param name="value">The attribute value to test.</param>
    /// <returns>Never returns; throws at runtime.</returns>
    /// <exception cref="InvalidOperationException">Always thrown — server-side only.</exception>
    public static bool IsNotMissing(this DbFunctions _, object? value)
        => throw new InvalidOperationException(
            "IsNotMissing is a server-side only method and cannot be called directly.");
}
