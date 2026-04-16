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

    /// <summary>Maps to <c>profile</c> as an owned nested map via snake_case convention.</summary>
    public SnakeCaseProfile? Profile { get; set; }
}

/// <summary>Owned profile shape for <see cref="SnakeCaseItem" />.</summary>
public sealed record SnakeCaseProfile
{
    /// <summary>Maps to <c>display_name</c> via snake_case convention.</summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>Maps to <c>preferred_address</c> as a nested owned map.</summary>
    public SnakeCaseAddress? PreferredAddress { get; set; }
}

/// <summary>Owned address shape for <see cref="SnakeCaseProfile" />.</summary>
public sealed record SnakeCaseAddress
{
    /// <summary>Maps to <c>city_name</c> via snake_case convention.</summary>
    public string CityName { get; set; } = null!;

    /// <summary>Maps to <c>geo_point</c> as a nested owned map.</summary>
    public SnakeCaseGeoPoint? GeoPoint { get; set; }
}

/// <summary>Owned geo shape for <see cref="SnakeCaseAddress" />.</summary>
public sealed record SnakeCaseGeoPoint
{
    /// <summary>Maps to <c>latitude_value</c> via snake_case convention.</summary>
    public decimal LatitudeValue { get; set; }
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
