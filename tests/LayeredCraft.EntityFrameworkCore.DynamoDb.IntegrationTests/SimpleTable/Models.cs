using Amazon.DynamoDBv2.Model;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

using System;

public sealed class SimpleItem
{
    // Partition key
    public string Pk { get; set; }

    // Boolean
    public bool BoolValue { get; set; }

    // Integral numerics
    public byte ByteValue { get; set; }
    public short ShortValue { get; set; }
    public int IntValue { get; set; }
    public long LongValue { get; set; }

    // Floating point / decimal
    public float FloatValue { get; set; }
    public double DoubleValue { get; set; }
    public decimal DecimalValue { get; set; }

    // String-like
    public string StringValue { get; set; } = null!;
    public Guid GuidValue { get; set; }

    // Temporal
    public DateTime DateTimeValue { get; set; }
    public DateTimeOffset DateTimeOffsetValue { get; set; }
}

public static class SimpleItemExtensions
{
    private static readonly List<Dictionary<string, AttributeValue>> Items =
    [
        new()
        {
            ["Pk"] = new AttributeValue("ITEM#1"),
            ["BoolValue"] = new AttributeValue { BOOL = true },
            ["ByteValue"] = new AttributeValue { N = "1" },
            ["ShortValue"] = new AttributeValue { N = "10" },
            ["IntValue"] = new AttributeValue { N = "100" },
            ["LongValue"] = new AttributeValue { N = "1000" },
            ["FloatValue"] = new AttributeValue { N = "1.5" },
            ["DoubleValue"] = new AttributeValue { N = "1.25" },
            ["DecimalValue"] = new AttributeValue { N = "10.123" },
            ["StringValue"] = new AttributeValue("alpha"),
            ["GuidValue"] = new AttributeValue("11111111-1111-1111-1111-111111111111"),
            ["DateTimeValue"] = new AttributeValue("2026-01-01T10:00:00Z"),
            ["DateTimeOffsetValue"] = new AttributeValue("2026-01-01T10:00:00+00:00"),
        },
        new()
        {
            ["Pk"] = new AttributeValue("ITEM#2"),
            ["BoolValue"] = new AttributeValue { BOOL = false },
            ["ByteValue"] = new AttributeValue { N = "255" },
            ["ShortValue"] = new AttributeValue { N = "32767" },
            ["IntValue"] = new AttributeValue { N = "200000" },
            ["LongValue"] = new AttributeValue { N = "9000000000" },
            ["FloatValue"] = new AttributeValue { N = "3.14" },
            ["DoubleValue"] = new AttributeValue { N = "2.718281828" },
            ["DecimalValue"] = new AttributeValue { N = "99999.9999" },
            ["StringValue"] = new AttributeValue("bravo"),
            ["GuidValue"] = new AttributeValue("22222222-2222-2222-2222-222222222222"),
            ["DateTimeValue"] = new AttributeValue("2026-01-02T11:30:00Z"),
            ["DateTimeOffsetValue"] = new AttributeValue("2026-01-02T11:30:00+00:00"),
        },
        new()
        {
            ["Pk"] = new AttributeValue("ITEM#3"),
            ["BoolValue"] = new AttributeValue { BOOL = true },
            ["ByteValue"] = new AttributeValue { N = "42" },
            ["ShortValue"] = new AttributeValue { N = "1234" },
            ["IntValue"] = new AttributeValue { N = "987654" },
            ["LongValue"] = new AttributeValue { N = "1234567890123" },
            ["FloatValue"] = new AttributeValue { N = "0.125" },
            ["DoubleValue"] = new AttributeValue { N = "0.333333333333" },
            ["DecimalValue"] = new AttributeValue { N = "0.0001" },
            ["StringValue"] = new AttributeValue("charlie"),
            ["GuidValue"] = new AttributeValue("33333333-3333-3333-3333-333333333333"),
            ["DateTimeValue"] = new AttributeValue("2026-01-03T18:45:30Z"),
            ["DateTimeOffsetValue"] = new AttributeValue("2026-01-03T18:45:30+00:00"),
        },
        new()
        {
            ["Pk"] = new AttributeValue("ITEM#4"),
            ["BoolValue"] = new AttributeValue { BOOL = false },
            ["ByteValue"] = new AttributeValue { N = "0" },
            ["ShortValue"] = new AttributeValue { N = "-1" },
            ["IntValue"] = new AttributeValue { N = "-100" },
            ["LongValue"] = new AttributeValue { N = "-1000000" },
            ["FloatValue"] = new AttributeValue { N = "-1.75" },
            ["DoubleValue"] = new AttributeValue { N = "-99.999" },
            ["DecimalValue"] = new AttributeValue { N = "-12345.6789" },
            ["StringValue"] = new AttributeValue("delta"),
            ["GuidValue"] = new AttributeValue("44444444-4444-4444-4444-444444444444"),
            ["DateTimeValue"] = new AttributeValue("2026-01-04T23:59:59Z"),
            ["DateTimeOffsetValue"] = new AttributeValue("2026-01-04T23:59:59+00:00"),
        },
    ];

    extension(SimpleItem)
    {
        public static List<Dictionary<string, AttributeValue>> GetSampleData() => Items;
    }
}
