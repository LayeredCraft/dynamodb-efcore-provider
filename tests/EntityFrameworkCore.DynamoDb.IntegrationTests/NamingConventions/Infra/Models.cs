namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventions.Infra;

/// <summary>Represents the QuestionItem type.</summary>
public sealed record QuestionItem
{
    private string _pk = null!;
    private string _sk = null!;
    private string? _gs1Pk;
    private string? _gs1Sk;
    private string? _gs2Pk;
    private string? _gs2Sk;

    public string Pk => _pk;
    public string Sk => _sk;

    public Guid Id
    {
        get;
        set
        {
            field = value;
            RecomputeKeys();
        }
    }

    public required string Message { get; set; }
    public required string RecordType
    {
        get;
        set
        {
            field = value ?? throw new ArgumentNullException(nameof(value));
            RecomputeKeys();
        }
    }
    public required string Game
    {
        get;
        set
        {
            field = value ?? throw new ArgumentNullException(nameof(value));
            RecomputeKeys();
        }
    }

    public List<AnswerItem> Answers { get; set; } =
    [
        new() { Message = string.Empty },
        new() { Message = string.Empty },
        new() { Message = string.Empty },
        new() { Message = string.Empty }
    ];
    public DateTimeOffset DateSubmitted { get; set; } = DateTimeOffset.UtcNow;
    public string? CategoryId
    {
        get;
        set
        {
            field = value;
            RecomputeKeys();
        }
    }
    // TODO: After migration and ensuring this is always set, make this non-nullable.
    public List<string>? Tags { get; set; } = [];
    public string? Gs1Pk => _gs1Pk;
    public string? Gs1Sk => _gs1Sk;
    public string? Gs2Pk => _gs2Pk;
    public string? Gs2Sk => _gs2Sk;
    public int? BucketId
    {
        get;
        set
        {
            field = value;
            RecomputeKeys();
        }
    }

    public Guid? BucketKey
    {
        get;
        set
        {
            field = value;
            RecomputeKeys();
        }
    }
    private bool IsQuestionRecordType => string.Equals(RecordType, "question", StringComparison.OrdinalIgnoreCase);

    public void RecomputeKeys()
    {
        _pk = $"game#{Game}";
        _sk = $"record-type#{RecordType}#id#{Id}";

        _gs1Pk = IsQuestionRecordType ? $"game#{Game}#bucket#{BucketId}#" : null;
        _gs1Sk = IsQuestionRecordType ? $"key#{BucketKey}" : null;

        _gs2Pk = IsQuestionRecordType ? $"game#{Game}#category#{CategoryId ?? "general"}#" : null;
        _gs2Sk = IsQuestionRecordType ? $"bucket#{BucketId:00}#key#{BucketKey}" : null;
    }

    public record AnswerItem
    {
        public required string Message { get; set; }
    }
}
