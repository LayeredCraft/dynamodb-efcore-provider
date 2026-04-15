namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.PkSkTable;

/// <summary>Represents the PkSkItem type.</summary>
public sealed record PkSkItem
{
    /// <summary>Provides functionality for this member.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Sk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public bool IsTarget { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public string Category { get; set; } = null!;
}
