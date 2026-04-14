using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SimpleTable;

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
            NullableBoolValue = null,
            NullableIntValue = null,
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
            NullableBoolValue = true,
            NullableIntValue = 42,
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
            NullableBoolValue = false,
            NullableIntValue = -1,
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
            NullableBoolValue = null,
            NullableIntValue = null,
            NullableStringValue = null,
        },
    ];

    public static readonly List<Dictionary<string, AttributeValue>> AttributeValues =
        SimpleItemMapper.ToItems(Items);
}
