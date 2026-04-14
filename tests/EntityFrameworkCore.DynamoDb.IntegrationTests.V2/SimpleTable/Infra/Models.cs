namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public sealed record SimpleItem
{
    public string Pk { get; set; } = null!;

    public bool BoolValue { get; set; }

    public int IntValue { get; set; }

    public long LongValue { get; set; }

    public float FloatValue { get; set; }

    public double DoubleValue { get; set; }

    public decimal DecimalValue { get; set; }

    public string StringValue { get; set; } = null!;

    public Guid GuidValue { get; set; }

    public DateTimeOffset DateTimeOffsetValue { get; set; }

    public bool? NullableBoolValue { get; set; }

    public int? NullableIntValue { get; set; }

    public string? NullableStringValue { get; set; }
}
