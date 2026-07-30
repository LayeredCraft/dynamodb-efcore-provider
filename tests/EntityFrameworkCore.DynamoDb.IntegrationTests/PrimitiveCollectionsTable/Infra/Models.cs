namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public record PrimitiveCollectionsItem(
    string Pk,
    List<string> Tags,
    Dictionary<string, int> ScoresByCategory,
    HashSet<string> LabelSet,
    HashSet<int> RatingSet,
    Dictionary<string, string> Metadata,
    List<string>? OptionalTags);
