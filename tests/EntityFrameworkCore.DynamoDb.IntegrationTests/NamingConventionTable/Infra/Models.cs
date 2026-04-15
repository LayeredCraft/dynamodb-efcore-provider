namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventionTable;

/// <summary>
///     Entity mapped with <see cref="DynamoAttributeNamingConvention.SnakeCase" />. All CLR
///     property names are transformed to snake_case, except <c>ExplicitOverride</c> which has a
///     per-property <c>HasAttributeName</c> override.
/// </summary>
public sealed record SnakeCaseItem
{
    /// <summary>Partition key — maps to DynamoDB attribute <c>pk</c>.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Maps to <c>first_name</c> via snake_case convention.</summary>
    public string FirstName { get; set; } = null!;

    /// <summary>Maps to <c>item_count</c> via snake_case convention.</summary>
    public int ItemCount { get; set; }

    /// <summary>
    ///     Stored as <c>custom_attr</c> in DynamoDB — explicit <c>HasAttributeName</c> overrides
    ///     the entity-level snake_case convention.
    /// </summary>
    public string ExplicitOverride { get; set; } = null!;
}

/// <summary>
///     Entity mapped with <see cref="DynamoAttributeNamingConvention.KebabCase" />. All CLR
///     property names are transformed to kebab-case.
/// </summary>
public sealed record KebabCaseItem
{
    /// <summary>Partition key — maps to DynamoDB attribute <c>pk</c>.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Maps to <c>display-name</c> via kebab-case convention.</summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>Maps to <c>total-count</c> via kebab-case convention.</summary>
    public int TotalCount { get; set; }
}
