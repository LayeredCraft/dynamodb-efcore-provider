namespace EntityFrameworkCore.DynamoDb.Metadata;

/// <summary>
///     Controls how CLR property names are automatically transformed into DynamoDB attribute
///     names when no explicit <c>HasAttributeName</c> override is present.
/// </summary>
/// <remarks>
///     Configure per entity type via
///     <c>modelBuilder.Entity&lt;T&gt;().HasAttributeNamingConvention(...)</c>. Complex types without
///     their own convention setting inherit the root entity's convention.
/// </remarks>
public enum DynamoAttributeNamingConvention
{
    /// <summary>
    ///     No transformation — property names are stored as-is (CLR property name). Equivalent to not
    ///     configuring a convention at all, but explicit. Useful to opt out of an inherited convention.
    /// </summary>
    None,

    /// <summary>
    ///     Converts property names to snake_case using Humanizer. For example: <c>FirstName</c> →
    ///     <c>first_name</c>.
    /// </summary>
    SnakeCase,

    /// <summary>
    ///     Converts property names to camelCase using Humanizer. For example: <c>FirstName</c> →
    ///     <c>firstName</c>.
    /// </summary>
    CamelCase,

    /// <summary>
    ///     Converts property names to kebab-case using Humanizer. For example: <c>FirstName</c> →
    ///     <c>first-name</c>.
    /// </summary>
    KebabCase,

    /// <summary>
    ///     Converts property names to UPPER_SNAKE_CASE using Humanizer. For example: <c>FirstName</c>
    ///     → <c>FIRST_NAME</c>.
    /// </summary>
    UpperSnakeCase,
}
