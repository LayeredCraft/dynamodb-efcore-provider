namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventionTable;

/// <summary>
///     Root entity for naming convention integration tests. Properties use PascalCase CLR names;
///     the DbContext configures snake_case as the attribute naming convention.
/// </summary>
public sealed record NamingConventionItem
{
    /// <summary>Partition key — maps to DynamoDB attribute <c>pk</c>.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Multi-word string property — maps to <c>first_name</c> via snake_case convention.</summary>
    public string FirstName { get; set; } = null!;

    /// <summary>Multi-word string property — maps to <c>last_name</c> via snake_case convention.</summary>
    public string LastName { get; set; } = null!;

    /// <summary>Integer property — maps to <c>item_count</c> via snake_case convention.</summary>
    public int ItemCount { get; set; }

    /// <summary>Boolean property — maps to <c>is_active</c> via snake_case convention.</summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Property with an explicit <c>HasAttributeName</c> override. Stored as <c>custom_attr</c>
    ///     in DynamoDB regardless of the entity-level snake_case convention.
    /// </summary>
    public string ExplicitOverride { get; set; } = null!;
}
