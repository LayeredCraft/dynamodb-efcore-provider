using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

public class ConverterAndBinarySerializationTests(SaveChangesTableDynamoFixture fixture)
    : SaveChangesTableTestBase(fixture)
{
    [Fact]
    public async Task ConverterCoverageItem_SaveChanges_WritesCustomConvertedValuesAndBinaryShapes()
    {
        var payload = new byte[] { 0, 1, 2, 3 };
        var firstTag = new byte[] { 10, 11 };
        var secondTag = new byte[] { 12, 13 };
        var firstHistory = new DateTimeOffset(2026, 4, 5, 12, 30, 0, TimeSpan.Zero);
        var secondHistory = new DateTimeOffset(2026, 4, 6, 13, 45, 0, TimeSpan.Zero);

        var entity = new ConverterCoverageItem
        {
            Pk = "TEST#CONV",
            Sk = "CONVERTER#WRITE-1",
            Version = 1,
            ExternalId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            OccurredAt = new DateTimeOffset(2026, 4, 4, 10, 15, 0, TimeSpan.Zero),
            Payload = payload,
            BinaryTags = new HashSet<byte[]>([firstTag, secondTag], ByteArrayComparer.Instance),
            History = [firstHistory, secondHistory],
        };

        Db.ConverterCoverageItems.Add(entity);
        await Db.SaveChangesAsync(CancellationToken);
        AssertSql(
            """
            INSERT INTO "AppItems"
            VALUE {'Pk': ?, 'Sk': ?, '$type': ?, 'BinaryTags': ?, 'ExternalId': ?, 'History': ?, 'OccurredAt': ?, 'Payload': ?, 'Version': ?}
            """);

        var rawItem = await GetItemAsync(entity.Pk, entity.Sk, CancellationToken);
        var actual = rawItem?.ToConverterCoverageItem();
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(entity);
    }

    [Fact]
    public async Task ConverterCoverageItem_Query_MaterializesCustomConvertedValuesAndBinaryShapes()
    {
        var payload = new byte[] { 5, 6, 7, 8 };
        var firstTag = new byte[] { 14, 15 };
        var secondTag = new byte[] { 16, 17 };
        var externalId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var occurredAt = new DateTimeOffset(2026, 4, 7, 9, 0, 0, TimeSpan.Zero);
        var firstHistory = new DateTimeOffset(2026, 4, 7, 10, 0, 0, TimeSpan.Zero);
        var secondHistory = new DateTimeOffset(2026, 4, 7, 11, 0, 0, TimeSpan.Zero);
        const string pk = "TEST#CONV";
        const string sk = "CONVERTER#READ-1";

        await PutItemAsync(
            new Dictionary<string, AttributeValue>
            {
                ["Pk"] = new() { S = pk },
                ["Sk"] = new() { S = sk },
                ["Version"] = new() { N = "1" },
                ["ExternalId"] = new() { S = externalId.ToString("N") },
                ["OccurredAt"] = new() { S = occurredAt.ToString("yyyy-MM-dd HH:mm:sszzz") },
                ["Payload"] = new() { B = new MemoryStream(payload, false) },
                ["BinaryTags"] =
                    new()
                    {
                        BS =
                        [
                            new MemoryStream(firstTag, false),
                            new MemoryStream(secondTag, false),
                        ],
                    },
                ["History"] = new()
                {
                    L =
                    [
                        new AttributeValue
                        {
                            S = firstHistory.ToString("yyyy-MM-dd HH:mm:sszzz"),
                        },
                        new AttributeValue
                        {
                            S = secondHistory.ToString("yyyy-MM-dd HH:mm:sszzz"),
                        },
                    ],
                },
                ["$type"] = new() { S = nameof(ConverterCoverageItem) },
            },
            CancellationToken);

        var entity =
            await Db
                .ConverterCoverageItems
                .Where(x => x.Pk == pk && x.Sk == sk)
                .AsAsyncEnumerable()
                .SingleAsync(CancellationToken);

        entity.ExternalId.Should().Be(externalId);
        entity.OccurredAt.Should().Be(occurredAt);
        entity.Payload.Should().Equal(payload);
        entity.BinaryTags.Should().NotBeNull();
        entity.BinaryTags!
            .Should()
            .BeEquivalentTo(
                [firstTag, secondTag],
                options => options
                    .Using<byte[]>(context => context.Subject.Should().Equal(context.Expectation))
                    .WhenTypeIs<byte[]>());
        entity.History.Should().Equal(firstHistory, secondHistory);
    }

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static ByteArrayComparer Instance { get; } = new();

        public bool Equals(byte[]? x, byte[]? y)
            => ReferenceEquals(x, y)
                || (x is not null && y is not null && x.AsSpan().SequenceEqual(y));

        public int GetHashCode(byte[] obj)
        {
            var hash = new HashCode();
            foreach (var b in obj)
                hash.Add(b);
            return hash.ToHashCode();
        }
    }
}
