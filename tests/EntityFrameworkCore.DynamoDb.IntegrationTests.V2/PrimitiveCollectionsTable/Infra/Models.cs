namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.PrimitiveCollectionsTable;

/// <summary>Represents the PrimitiveCollectionsItem type.</summary>
public record PrimitiveCollectionsItem(
    string Pk,
    List<string> Tags,
    Dictionary<string, int> ScoresByCategory,
    HashSet<string> LabelSet,
    HashSet<int> RatingSet,
    Dictionary<string, string> Metadata,
    List<string>? OptionalTags);
