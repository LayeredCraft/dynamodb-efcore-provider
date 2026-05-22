using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingOverrideTable.Infra;

/// <summary>Deterministic seed items for naming convention tests.</summary>
public static class NamingConventionsItems
{
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
                new QuestionItem.AnswerItem { Message = "African or European?" },
                new QuestionItem.AnswerItem { Message = "About 24 mph." },
                new QuestionItem.AnswerItem { Message = "Roughly 11 m/s." },
                new QuestionItem.AnswerItem { Message = "It depends on the swallow." }
            ]
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
                new QuestionItem.AnswerItem { Message = "Red" },
                new QuestionItem.AnswerItem { Message = "Blue" },
                new QuestionItem.AnswerItem { Message = "Yellow" },
                new QuestionItem.AnswerItem { Message = "Green" }
            ]
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
                new QuestionItem.AnswerItem { Message = "Coordinated Universal Time." },
                new QuestionItem.AnswerItem
                {
                    Message = "Greenwich Mean Time equivalent for offsets."
                },
                new QuestionItem.AnswerItem { Message = "Offset +00:00." },
                new QuestionItem.AnswerItem { Message = "Zulu time." }
            ]
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
                new QuestionItem.AnswerItem { Message = "First option." },
                new QuestionItem.AnswerItem { Message = "Second option." },
                new QuestionItem.AnswerItem { Message = "Third option." },
                new QuestionItem.AnswerItem { Message = "Fourth option." }
            ]
        }
    ];

    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
        CreateAttributeValues();

    /// <summary>Converts deterministic test items to DynamoDB attribute maps.</summary>
    private static IReadOnlyList<Dictionary<string, AttributeValue>> CreateAttributeValues()
        => Items.Select(NamingConventionsItemMapper.ToItem).ToList();
}
