namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public record PrimitiveCollectionsItem(
    string Pk,
    List<string> Tags,
    Dictionary<string, int> ScoresByCategory,
    HashSet<string> LabelSet,
    HashSet<int> RatingSet,
    Dictionary<string, string> Metadata,
    List<string>? OptionalTags);

public class MutablePrimitiveCollectionsItem
{
    public string Pk { get; set; } = string.Empty;

    public Dictionary<string, decimal> ChargesByTier { get; set; } = [];

    public HashSet<int> RatingSet { get; set; } = [];

    public List<string> Tags { get; set; } = [];

    public List<int?>? OptionalScores { get; set; }
}
