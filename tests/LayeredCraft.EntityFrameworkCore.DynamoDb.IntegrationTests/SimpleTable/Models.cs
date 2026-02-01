using Amazon.DynamoDBv2.Model;
using DynamoMapper.Runtime;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

using System;

public sealed record SimpleItem
{
    // Partition key
    public string Pk { get; set; } = null!;

    // Boolean
    public bool BoolValue { get; set; }

    // Integral numerics
    // public byte ByteValue { get; set; }
    // public short ShortValue { get; set; }
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
    public DateTimeOffset DateTimeOffsetValue { get; set; }

    // nullable
    public string? NullableStringValue { get; set; }
}

public static class SimpleItems
{
    public static readonly List<SimpleItem> Items =
    [
        new()
        {
            Pk = "ITEM#1",
            BoolValue = true,
            IntValue = 100,
            LongValue = 1000,
            FloatValue = 1.5f,
            DoubleValue = 1.25,
            DecimalValue = 10.123m,
            StringValue = "alpha",
            GuidValue = new Guid("11111111-1111-1111-1111-111111111111"),
            DateTimeOffsetValue = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
            NullableStringValue = null,
        },
        new()
        {
            Pk = "ITEM#2",
            BoolValue = false,
            IntValue = 200000,
            LongValue = 9000000000,
            FloatValue = 3.14f,
            DoubleValue = 2.718281828,
            DecimalValue = 99999.9999m,
            StringValue = "bravo",
            GuidValue = new Guid("22222222-2222-2222-2222-222222222222"),
            DateTimeOffsetValue = new DateTimeOffset(2026, 1, 2, 11, 30, 0, TimeSpan.Zero),
            NullableStringValue = "Null String",
        },
        new()
        {
            Pk = "ITEM#3",
            BoolValue = true,
            IntValue = 987654,
            LongValue = 1234567890123,
            FloatValue = 0.125f,
            DoubleValue = 0.333333333333,
            DecimalValue = 0.0001m,
            StringValue = "charlie",
            GuidValue = new Guid("33333333-3333-3333-3333-333333333333"),
            DateTimeOffsetValue = new DateTimeOffset(2026, 1, 3, 18, 45, 30, TimeSpan.Zero),
            NullableStringValue = null,
        },
        new()
        {
            Pk = "ITEM#4",
            BoolValue = false,
            IntValue = -100,
            LongValue = -1000000,
            FloatValue = -1.75f,
            DoubleValue = -99.999,
            DecimalValue = -12345.6789m,
            StringValue = "delta",
            GuidValue = new Guid("44444444-4444-4444-4444-444444444444"),
            DateTimeOffsetValue = new DateTimeOffset(2026, 1, 4, 23, 59, 59, TimeSpan.Zero),
            NullableStringValue = null,
        },
    ];

    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
        SimpleItemMapper.ToItems(Items);
}

[DynamoMapper(Convention = DynamoNamingConvention.Exact)]
internal static partial class SimpleItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(SimpleItem source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(List<SimpleItem> sources)
        => sources.Select(ToItem).ToList();

    internal static partial SimpleItem FromItem(Dictionary<string, AttributeValue> item);

    internal static List<SimpleItem> FromItems(List<Dictionary<string, AttributeValue>> items)
        => items.Select(FromItem).ToList();
}
