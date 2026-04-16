using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventions.Infra;

/// <summary>Deterministic seed items for naming convention tests.</summary>
public static class NamingConventionsItems
{
    /// <summary>Provides functionality for this member.</summary>
    public static readonly List<QuestionItem> Items =
    [
        new()
        {
            Game = "trivia",
            RecordType = "question",
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Message = "What is the airspeed velocity of an unladen swallow?",
            DateSubmitted = new DateTimeOffset(2026, 01, 01, 8, 30, 0, TimeSpan.Zero),
            CategoryId = "general",
            Tags = ["featured", "vip"],
            BucketId = 1,
            BucketKey = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Answers =
            [
                new() { Message = "African or European?" },
                new() { Message = "About 24 mph." },
                new() { Message = "Roughly 11 m/s." },
                new() { Message = "It depends on the swallow." },
            ],
        },
        new()
        {
            Game = "trivia",
            RecordType = "question",
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Message = "Name a primary color.",
            DateSubmitted = new DateTimeOffset(2026, 01, 05, 9, 15, 0, TimeSpan.Zero),
            CategoryId = null,
            Tags = ["new"],
            BucketId = 2,
            BucketKey = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Answers =
            [
                new() { Message = "Red" },
                new() { Message = "Blue" },
                new() { Message = "Yellow" },
                new() { Message = "Green" },
            ],
        },
        new()
        {
            Game = "trivia",
            RecordType = "question",
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Message = "What time zone is UTC?",
            DateSubmitted = new DateTimeOffset(2026, 01, 09, 18, 45, 0, TimeSpan.Zero),
            CategoryId = "general",
            Tags = [],
            BucketId = 3,
            BucketKey = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Answers =
            [
                new() { Message = "Coordinated Universal Time." },
                new() { Message = "Greenwich Mean Time equivalent for offsets." },
                new() { Message = "Offset +00:00." },
                new() { Message = "Zulu time." },
            ],
        },
        new()
        {
            Game = "trivia",
            RecordType = "question",
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Message = "Edge case: null category and empty tags.",
            DateSubmitted = new DateTimeOffset(2026, 01, 12, 5, 0, 0, TimeSpan.Zero),
            CategoryId = null,
            Tags = [],
            BucketId = null,
            BucketKey = null,
            Answers =
            [
                new() { Message = "First option." },
                new() { Message = "Second option." },
                new() { Message = "Third option." },
                new() { Message = "Fourth option." },
            ],
        },
    ];

    /// <summary>Provides functionality for this member.</summary>
    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
        CreateAttributeValues();

    /// <summary>Converts deterministic test items to DynamoDB attribute maps.</summary>
    private static IReadOnlyList<Dictionary<string, AttributeValue>> CreateAttributeValues()
        => Items.Select(NamingConventionsItemMapper.ToItem).ToList();
}
