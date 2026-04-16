namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

/// <summary>Represents the PkSkItem type.</summary>
public sealed record PkSkItem
{
    public string Pk { get; set; } = null!;

    public string Sk { get; set; } = null!;

    public bool IsTarget { get; set; }

    public string Category { get; set; } = null!;
}
